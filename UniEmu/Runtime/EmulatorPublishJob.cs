using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Data;
using UniEmu.Features.Contracts;

namespace UniEmu.Runtime;

[DisallowConcurrentExecution]
public sealed class EmulatorPublishJob(
    UniEmuDbContext db,
    TelemetryValueGenerator valueGenerator,
    TagRuntimeStateStore stateStore,
    TelemetryPacketSender sender,
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
        var generatedValues = BuildValues(emulator, now);
        var telemetryValues = generatedValues
            .Where(value => value.NumericValue is not null)
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().NumericValue!.Value, StringComparer.OrdinalIgnoreCase);
        var dispatcherValues = generatedValues
            .Select(value => new UniversalValue(value.Key, value.Value))
            .ToList();
        var machineIntegrationId = GetMachineIntegrationId(emulator);
        var mainProgram = ResolveProgram(emulator.Id, generatedValues, SpecialParameter.PrgName);
        var subProgram = ResolveProgram(emulator.Id, generatedValues, SpecialParameter.Subprogram);
        var level = EventLevel.Success;
        var message = "Пакет мониторинга отправлен в Dispatcher";

        db.TelemetryPoints.Add(new TelemetryPointEntity
        {
            EmulatorId = emulator.Id,
            Timestamp = now,
            ValuesJson = UniEmuJson.Serialize(telemetryValues),
        });

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

        db.SystemEvents.Add(new SystemEventEntity
        {
            Id = $"ev-{Guid.NewGuid():N}"[..12],
            EmulatorId = emulator.Id,
            EmulatorName = emulator.Name,
            Level = UniEmuJson.EnumString(level),
            Message = message,
            Timestamp = now,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private IReadOnlyList<GeneratedTagValue> BuildValues(EmulatorEntity emulator, DateTimeOffset timestamp)
    {
        var values = new List<GeneratedTagValue>();

        foreach (var tag in emulator.Tags)
        {
            var generated = valueGenerator.GenerateTag(emulator, tag, timestamp);
            if (stateStore.TryGet(emulator.Id, tag.Id, out var runtimeValue))
            {
                values.Add(generated with
                {
                    Value = runtimeValue.Value,
                    NumericValue = runtimeValue.Value,
                });
                continue;
            }

            if (generated.NumericValue is not null)
            {
                stateStore.Set(emulator.Id, tag.Id, tag.Name, generated.NumericValue.Value, timestamp);
            }

            values.Add(generated);
        }

        return values;
    }

    private static object GetMachineIntegrationId(EmulatorEntity emulator)
    {
        var digits = new string(emulator.Id.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : emulator.Id;
    }

    private DispatcherProgram? ResolveProgram(
        string emulatorId,
        IReadOnlyList<GeneratedTagValue> values,
        SpecialParameter specialParameter)
    {
        var programName = values
            .FirstOrDefault(value => value.SpecialParameter == specialParameter)
            ?.Value
            ?.ToString();

        if (string.IsNullOrWhiteSpace(programName))
        {
            return null;
        }

        var match = db.CncPrograms
            .AsEnumerable()
            .Where(program => program.Name.Equals(programName, StringComparison.OrdinalIgnoreCase))
            .Where(program => program.Scope == UniEmuJson.EnumString(CncScope.Shared) || program.EmulatorId == emulatorId)
            .OrderByDescending(program => program.Description.StartsWith("[dispatcher-received]", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(program => program.EmulatorId == emulatorId)
            .FirstOrDefault();

        return match is null ? null : TelemetryPacketSender.FromTextProgram(match.Name, match.Content);
    }

    private async Task HandleDispatcherAnswerAsync(
        EmulatorEntity emulator,
        object machineIntegrationId,
        DispatcherProgram? mainProgram,
        DispatcherProgram? subProgram,
        string answer,
        CancellationToken cancellationToken)
    {
        if (answer.Contains("FileType=1", StringComparison.OrdinalIgnoreCase))
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, mainProgram, cancellationToken);
        }

        if (answer.Contains("FileType=2", StringComparison.OrdinalIgnoreCase))
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, subProgram, cancellationToken);
        }

        if (answer.Contains("GetFile=1", StringComparison.OrdinalIgnoreCase))
        {
            var received = await sender.ReceiveProgramAsync(emulator.TargetUrl, machineIntegrationId, cancellationToken);
            if (received is not null)
            {
                UpsertReceivedProgram(emulator.Id, received);
            }
        }
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
            return;
        }

        existing.Description = "[dispatcher-received] Получена от Dispatcher";
        existing.Content = content;
        existing.SizeBytes = program.Bytes.Length;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
