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
        var values = BuildValues(emulator, now);
        var packet = new TelemetryPacket(emulator.Id, now, values);
        var level = EventLevel.Success;
        var message = "Пакет телеметрии отправлен";

        db.TelemetryPoints.Add(new TelemetryPointEntity
        {
            EmulatorId = emulator.Id,
            Timestamp = now,
            ValuesJson = UniEmuJson.Serialize(values),
        });

        try
        {
            await sender.SendAsync(emulator.TargetUrl, packet, cancellationToken);
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

    private IReadOnlyDictionary<string, double> BuildValues(EmulatorEntity emulator, DateTimeOffset timestamp)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in emulator.Tags)
        {
            if (stateStore.TryGet(emulator.Id, tag.Id, out var runtimeValue))
            {
                values[tag.Name] = runtimeValue.Value;
                continue;
            }

            var value = valueGenerator.GenerateTag(emulator, tag, timestamp);
            stateStore.Set(emulator.Id, tag.Id, tag.Name, value, timestamp);
            values[tag.Name] = value;
        }

        return values;
    }
}
