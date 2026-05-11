using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Runtime;

[DisallowConcurrentExecution]
public sealed class EmulatorPublishJob(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TelemetryValueGenerator valueGenerator,
    TagScriptExecutionService scriptExecutionService,
    TagRuntimeStateStore stateStore,
    TelemetryPacketSender sender,
    RuntimeUpdateService runtimeUpdateService,
    ILogger<EmulatorPublishJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var emulatorId = context.MergedJobDataMap.GetString(RuntimeJobKeys.EmulatorId);
        if (string.IsNullOrWhiteSpace(emulatorId))
        {
            logger.LogWarning("Publish job is missing emulatorId");
            return;
        }

        var emulator = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (emulator is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.ClearEmulator(emulatorId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var scheduledAt = context.ScheduledFireTimeUtc ?? now;
        var generatedValues = await BuildValuesAsync(emulator, now, scheduledAt, cancellationToken);
        var telemetryValues = BuildTelemetryValues(generatedValues);

        var dispatcherValues = BuildDispatcherValues(emulator.Tags, generatedValues);
        var machineIntegrationId = GetMachineIntegrationId(emulator);
        var mainProgram = await ResolveProgramAsync(emulator.Id, generatedValues, SpecialParameter.PrgName, cancellationToken);
        var subProgram = await ResolveProgramAsync(emulator.Id, generatedValues, SpecialParameter.Subprogram, cancellationToken);

        var level = EventLevel.Success;
        var message = string.Empty;

        var telemetryPoint = new TelemetryPointEntity
        {
            EmulatorId = emulator.Id,
            Timestamp = now,
            ValuesJson = UniEmuJson.Serialize(telemetryValues),
        };
        db.TelemetryPoints.Add(telemetryPoint);

        try
        {
            var request = new UniversalPostRequest(machineIntegrationId, UseInnerId: true, dispatcherValues);
            var answer = await sender.SendMonitoringAsync(emulator.TargetUrl, request, cancellationToken);
            await HandleDispatcherAnswerAsync(emulator, machineIntegrationId, mainProgram, subProgram, answer, cancellationToken);
            emulator.LastError = null;
            emulator.TotalRequests++;
        }
        catch (Exception ex)
        {
            level = EventLevel.Error;
            message = $"Ошибка отправки телеметрии: {ex.Message}";
            emulator.LastError = ex.Message;
            logger.LogWarning(ex, "Telemetry send failed for emulator {EmulatorId}", emulator.Id);
        }

        emulator.LastRun = now;
        emulator.NextRun = now.AddSeconds(Math.Max(1, emulator.IntervalSec));
        emulator.StartedAt ??= now;

        var systemEvent = default(SystemEventEntity);
        if (level == EventLevel.Error)
        {
            systemEvent = new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(level),
                Message = message,
                Timestamp = now,
            };
            db.SystemEvents.Add(systemEvent);
        }

        await db.SaveChangesAsync(cancellationToken);

        await runtimeUpdateService.PublishTelemetryAsync(emulator.Id, telemetryPoint.ToDto(), cancellationToken);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(emulator.ToDto(emulator.Tags.Count), cancellationToken);

        if (systemEvent is not null)
            await runtimeUpdateService.PublishEventCreatedAsync(systemEvent.ToDto(), cancellationToken);
    }

    private async Task<IReadOnlyList<GeneratedTagValue>> BuildValuesAsync(
        EmulatorEntity emulator,
        DateTimeOffset timestamp,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        var values = new List<GeneratedTagValue>();

        foreach (var tag in emulator.Tags)
        {
            if (ShouldWaitForScheduledValue(emulator, tag))
            {
                var freshValue = await stateStore.WaitForValueAsync(
                    emulator.Id,
                    tag.Id,
                    scheduledAt.AddMilliseconds(-50),
                    GetCalculationWaitTimeout(emulator),
                    cancellationToken);

                if (freshValue is not null)
                {
                    values.Add(FromRuntimeValue(tag, freshValue));
                    continue;
                }
            }

            if (stateStore.TryGet(emulator.Id, tag.Id, out var runtimeValue) && ShouldUseStoredValue(emulator, tag))
            {
                values.Add(FromRuntimeValue(tag, runtimeValue));
                continue;
            }

            if (ShouldUsePersistedPreview(tag))
            {
                var persisted = FromPersistedPreview(tag);
                stateStore.Set(emulator.Id, tag.Id, tag.Name, persisted.Value, persisted.NumericValue, timestamp);
                values.Add(persisted);
                continue;
            }

            var generated = await GenerateTagAsync(emulator, tag, timestamp, cancellationToken);
            tag.Preview = TelemetryValueGenerator.ToPreview(generated.Value);
            stateStore.Set(emulator.Id, tag.Id, tag.Name, generated.Value, generated.NumericValue, timestamp);

            values.Add(generated);
        }

        values = await ApplySpecializedProgramValuesAsync(emulator, values, timestamp, cancellationToken);

        return values;
    }

    public static List<UniversalValue> BuildDispatcherValues(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<GeneratedTagValue> generatedValues)
    {
        return tags
            .Zip(generatedValues)
            .Where(pair => pair.First.Enabled)
            .Select(pair => new UniversalValue(pair.Second.Key, pair.Second.Value))
            .ToList();
    }

    public static IReadOnlyDictionary<string, object?> BuildTelemetryValues(
        IReadOnlyList<GeneratedTagValue> generatedValues)
    {
        return generatedValues
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    private static GeneratedTagValue FromRuntimeValue(EmulatorTagEntity tag, TagRuntimeValue runtimeValue)
    {
        return new GeneratedTagValue(
            tag.Key,
            tag.Name,
            runtimeValue.Value,
            runtimeValue.NumericValue,
            GetSpecialParameter(tag));
    }

    private static GeneratedTagValue FromPersistedPreview(EmulatorTagEntity tag)
    {
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = TelemetryValueGenerator.FromPreview(tagType, tag.Preview);

        return new GeneratedTagValue(
            tag.Key,
            tag.Name,
            value,
            TelemetryValueGenerator.ToNumericValue(value),
            GetSpecialParameter(tag));
    }

    private async Task<GeneratedTagValue> GenerateTagAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source is not (TagSource.Script or TagSource.Formula))
        {
            return valueGenerator.GenerateTag(emulator, tag, timestamp);
        }

        try
        {
            return await scriptExecutionService.GenerateScriptTagAsync(emulator, tag, timestamp, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Script tag generation failed for tag {TagId}", tag.Id);
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = $"Ошибка вычисления скрипта тега {tag.Name}: {ex.Message}",
                Timestamp = timestamp,
            });

            return valueGenerator.GenerateTag(emulator, tag, timestamp);
        }
    }

    private async Task<List<GeneratedTagValue>> ApplySpecializedProgramValuesAsync(
        EmulatorEntity emulator,
        List<GeneratedTagValue> values,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (!emulator.Tags.Any(IsCalculatedProgramFrameTag))
        {
            return values;
        }

        var programs = await dataCache.GetVisibleCncProgramsAsync(emulator.Id, cancellationToken);
        var mainProgram = ResolveProgram(programs, emulator.Id, GetSpecialStringValue(values, SpecialParameter.PrgName));
        var subProgram = ResolveProgram(programs, emulator.Id, GetSpecialStringValue(values, SpecialParameter.Subprogram));
        var frame = CalculateProgramFrame(emulator, timestamp, subProgram ?? mainProgram);
        var enriched = values.ToList();

        for (var i = 0; i < emulator.Tags.Count && i < enriched.Count; i++)
        {
            var tag = emulator.Tags[i];
            if (!IsCalculatedProgramFrameTag(tag))
            {
                continue;
            }

            var specialParameter = GetSpecialParameter(tag);
            object value = specialParameter == SpecialParameter.FrameNum
                ? frame.Number
                : frame.Text;

            var generated = new GeneratedTagValue(
                enriched[i].Key,
                enriched[i].Name,
                value,
                TelemetryValueGenerator.ToNumericValue(value),
                specialParameter);

            enriched[i] = generated;
            tag.Preview = TelemetryValueGenerator.ToPreview(value);
            stateStore.Set(emulator.Id, tag.Id, tag.Name, generated.Value, generated.NumericValue, timestamp);
        }

        return enriched;
    }

    private static bool ShouldUseStoredValue(EmulatorEntity emulator, EmulatorTagEntity tag)
    {
        var trigger = GetTrigger(tag);

        if (trigger.Mode != TagTriggerMode.Interval)
        {
            return true;
        }

        var tagInterval = ToTimeSpan(trigger);
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        return Math.Abs((tagInterval - publishInterval).TotalMilliseconds) > 0.5;
    }

    private static bool ShouldWaitForScheduledValue(EmulatorEntity emulator, EmulatorTagEntity tag)
    {
        var trigger = GetTrigger(tag);

        if (trigger.Mode != TagTriggerMode.Interval)
        {
            return false;
        }

        var tagInterval = ToTimeSpan(trigger);
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        return Math.Abs((tagInterval - publishInterval).TotalMilliseconds) <= 0.5;
    }

    private static bool ShouldUsePersistedPreview(EmulatorTagEntity tag)
    {
        var trigger = GetTrigger(tag);
        return trigger.Mode is TagTriggerMode.Once or TagTriggerMode.Cron;
    }

    private static TagTriggerDto GetTrigger(EmulatorTagEntity tag)
    {
        return UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
               ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);
    }

    private static TimeSpan GetCalculationWaitTimeout(EmulatorEntity emulator)
    {
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        var timeoutMs = Math.Clamp(publishInterval.TotalMilliseconds * 0.75, 50, 5000);
        return TimeSpan.FromMilliseconds(timeoutMs);
    }

    private static SpecialParameter? GetSpecialParameter(EmulatorTagEntity tag)
    {
        return string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);
    }

    private static TimeSpan ToTimeSpan(TagTriggerDto trigger)
    {
        var value = Math.Max(1, trigger.IntervalValue ?? 1);
        return trigger.IntervalUnit switch
        {
            TagIntervalUnit.Ms => TimeSpan.FromMilliseconds(value),
            TagIntervalUnit.Min => TimeSpan.FromMinutes(value),
            _ => TimeSpan.FromSeconds(value),
        };
    }

    private static object GetMachineIntegrationId(EmulatorEntity emulator) => emulator.ProtocolId;

    private async Task<DispatcherProgram?> ResolveProgramAsync(
        string emulatorId,
        IReadOnlyList<GeneratedTagValue> values,
        SpecialParameter specialParameter,
        CancellationToken cancellationToken)
    {
        var programName = values
            .FirstOrDefault(value => value.SpecialParameter == specialParameter)
            ?.Value
            ?.ToString();

        if (string.IsNullOrWhiteSpace(programName))
        {
            return null;
        }

        var programs = await dataCache.GetVisibleCncProgramsAsync(emulatorId, cancellationToken);
        var match = ResolveProgram(programs, emulatorId, programName);

        return match is null ? null : TelemetryPacketSender.FromTextProgram(match.Name, match.Content);
    }

    private async Task HandleDispatcherAnswerAsync(
        EmulatorEntity emulator,
        object machineIntegrationId,
        DispatcherProgram? mainProgram,
        DispatcherProgram? subProgram,
        DispatcherMonitoringAnswer answer,
        CancellationToken cancellationToken)
    {
        if (answer.FileType == 1)
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, mainProgram, cancellationToken);
        }

        if (answer.FileType == 2)
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, subProgram, cancellationToken);
        }

        if (answer.GetFile == 1)
        {
            var received = await sender.ReceiveProgramAsync(emulator.TargetUrl, machineIntegrationId, cancellationToken);
            if (received is not null)
            {
                UpsertReceivedProgram(emulator.Id, received);
                StoreReceivedProgramName(emulator, received.Name);
            }
        }
    }

    private void StoreReceivedProgramName(EmulatorEntity emulator, string programName)
    {
        var tag = emulator.Tags.FirstOrDefault(tag =>
            tag.Enabled &&
            GetSpecialParameter(tag) == SpecialParameter.PrgName &&
            UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Static);

        if (tag is null)
            return;

        tag.Preview = programName;
        stateStore.Set(emulator.Id, tag.Id, tag.Name, programName, numericValue: null, DateTimeOffset.UtcNow);
    }

    private void UpsertReceivedProgram(string emulatorId, DispatcherProgram program)
    {
        var content = System.Text.Encoding.UTF8.GetString(program.Bytes);
        var existing = db.CncPrograms.FirstOrDefault(item =>
            item.EmulatorId == emulatorId &&
            item.Name == program.Name);

        if (existing is null)
        {
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = $"cnc-{Guid.NewGuid():N}"[..13],
                Name = program.Name,
                Scope = UniEmuJson.EnumString(CncScope.Emulator),
                EmulatorId = emulatorId,
                Description = "[dispatcher-received] Получена от Dispatcher",
                Content = content,
                SizeBytes = program.Bytes.Length,
                UpdatedAt = DateTimeOffset.UtcNow,
                UploadedAt = DateTimeOffset.UtcNow,
            });
            dataCache.InvalidateCncPrograms();
            return;
        }

        existing.Description = "[dispatcher-received] Получена от Dispatcher";
        existing.Content = content;
        existing.SizeBytes = program.Bytes.Length;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        dataCache.InvalidateCncPrograms();
    }

    private static CncProgramEntity? ResolveProgram(
        IReadOnlyList<CncProgramEntity> programs,
        string emulatorId,
        string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName))
        {
            return null;
        }

        return programs
            .Where(program => program.Name.Equals(programName, StringComparison.OrdinalIgnoreCase))
            .Where(program => program.Scope == UniEmuJson.EnumString(CncScope.Shared) || program.EmulatorId == emulatorId)
            .OrderByDescending(program => program.Description.StartsWith("[dispatcher-received]", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(program => program.EmulatorId == emulatorId)
            .FirstOrDefault();
    }

    private static string? GetSpecialStringValue(IReadOnlyList<GeneratedTagValue> values, SpecialParameter specialParameter)
    {
        return values
            .FirstOrDefault(value => value.SpecialParameter == specialParameter)
            ?.Value
            ?.ToString();
    }

    private static bool IsCalculatedProgramFrameTag(EmulatorTagEntity tag)
    {
        var specialParameter = GetSpecialParameter(tag);
        if (specialParameter is not (SpecialParameter.FrameNum or SpecialParameter.FrameText))
        {
            return false;
        }

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source is TagSource.Static or TagSource.Scenario;
    }

    private static ProgramFrame CalculateProgramFrame(
        EmulatorEntity emulator,
        DateTimeOffset timestamp,
        CncProgramEntity? program)
    {
        var lines = SplitProgramLines(program?.Content);
        if (lines.Length == 0)
        {
            return new ProgramFrame(0, string.Empty);
        }

        var elapsedSec = emulator.StartedAt is null
            ? 0
            : Math.Max(0, (timestamp - emulator.StartedAt.Value).TotalSeconds);
        var stepSeconds = Math.Max(1, emulator.IntervalSec);
        var frameNumber = (int)(Math.Floor(elapsedSec / stepSeconds) % lines.Length);

        return new ProgramFrame(frameNumber, lines[frameNumber]);
    }

    private static string[] SplitProgramLines(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            return lines[..^1];
        }

        return lines;
    }

    private sealed record ProgramFrame(int Number, string Text);
}
