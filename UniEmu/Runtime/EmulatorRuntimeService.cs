using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Features.Contracts;

namespace UniEmu.Runtime;

public sealed class EmulatorRuntimeService(
    IServiceScopeFactory scopeFactory,
    TelemetryValueGenerator valueGenerator,
    ILogger<EmulatorRuntimeService> logger) : BackgroundService
{
    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueEmulatorsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Runtime tick failed");
            }

            await Task.Delay(TickDelay, stoppingToken);
        }
    }

    private async Task RunDueEmulatorsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UniEmuDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<TelemetryPacketSender>();
        var now = DateTimeOffset.UtcNow;

        var emulators = (await db.Emulators
            .Include(e => e.Tags)
            .Where(e => e.Status == nameof(EmulatorStatus.Running))
            .ToListAsync(cancellationToken))
            .Where(e => e.NextRun is null || e.NextRun <= now)
            .ToList();

        foreach (var emulator in emulators)
        {
            var generatedValues = valueGenerator.GenerateTagValues(emulator, emulator.Tags, now);
            var telemetryValues = generatedValues
                .Where(value => value.NumericValue is not null)
                .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().NumericValue!.Value, StringComparer.OrdinalIgnoreCase);
            var dispatcherValues = generatedValues
                .Select(value => new UniversalValue(value.Key, value.Value))
                .ToList();
            var machineIntegrationId = GetMachineIntegrationId(emulator);
            var mainProgram = ResolveProgram(db.CncPrograms.Local.Concat(db.CncPrograms), emulator.Id, generatedValues, SpecialParameter.PrgName);
            var subProgram = ResolveProgram(db.CncPrograms.Local.Concat(db.CncPrograms), emulator.Id, generatedValues, SpecialParameter.Subprogram);
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
                await HandleDispatcherAnswerAsync(db, sender, emulator, machineIntegrationId, mainProgram, subProgram, answer, cancellationToken);
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
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static object GetMachineIntegrationId(EmulatorEntity emulator)
    {
        var digits = new string(emulator.Id.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : emulator.Id;
    }

    private static DispatcherProgram? ResolveProgram(
        IEnumerable<CncProgramEntity> programs,
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

        var candidates = programs
            .Where(program => program.Name.Equals(programName, StringComparison.OrdinalIgnoreCase))
            .Where(program => program.Scope == UniEmuJson.EnumString(CncScope.Shared) || program.EmulatorId == emulatorId)
            .OrderByDescending(program => program.Description.StartsWith("[dispatcher-received]", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(program => program.EmulatorId == emulatorId)
            .ToList();

        var match = candidates.FirstOrDefault();
        return match is null ? null : TelemetryPacketSender.FromTextProgram(match.Name, match.Content);
    }

    private static async Task HandleDispatcherAnswerAsync(
        UniEmuDbContext db,
        TelemetryPacketSender sender,
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
                UpsertReceivedProgram(db, emulator.Id, received);
            }
        }
    }

    private static void UpsertReceivedProgram(UniEmuDbContext db, string emulatorId, DispatcherProgram program)
    {
        var content = System.Text.Encoding.UTF8.GetString(program.Bytes);
        var existing = db.CncPrograms.Local.FirstOrDefault(item =>
            item.EmulatorId == emulatorId &&
            item.Name.Equals(program.Name, StringComparison.OrdinalIgnoreCase))
            ?? db.CncPrograms.FirstOrDefault(item =>
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
