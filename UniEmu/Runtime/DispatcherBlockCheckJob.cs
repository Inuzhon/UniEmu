using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Runtime;

[DisallowConcurrentExecution]
public sealed class DispatcherBlockCheckJob(
    UniEmuDbContext db,
    TelemetryPacketSender sender,
    RuntimeUpdateService runtimeUpdateService,
    ILogger<DispatcherBlockCheckJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var emulatorId = context.MergedJobDataMap.GetString(RuntimeJobKeys.EmulatorId);

        if (string.IsNullOrWhiteSpace(emulatorId))
        {
            logger.LogWarning("Dispatcher block check job is missing emulatorId");
            return;
        }

        var emulator = await db.Emulators
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (emulator is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            return;
        }

        var protocolId = emulator.ProtocolId;

        try
        {
            var isBlocked = await sender.GetIsMonitoringBlockedAsync(
                emulator.TargetUrl,
                protocolId,
                cancellationToken);

            if (!isBlocked)
            {
                return;
            }

            var message = $"Протокол {protocolId} заблокирован Dispatcher";
            emulator.Status = nameof(EmulatorStatus.Error);
            emulator.LastError = message;
            emulator.NextRun = null;

            var systemEvent = new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = message,
                Timestamp = DateTimeOffset.UtcNow,
            };
            db.SystemEvents.Add(systemEvent);

            await db.SaveChangesAsync(cancellationToken);
            await runtimeUpdateService.PublishEmulatorUpdatedAsync(emulator.ToDto(emulator.Tags.Count), cancellationToken);
            await runtimeUpdateService.PublishEventCreatedAsync(systemEvent.ToDto(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dispatcher block check failed for emulator {EmulatorId}", emulator.Id);
        }
    }
}
