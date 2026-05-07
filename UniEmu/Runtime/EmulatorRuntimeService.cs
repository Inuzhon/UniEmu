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
            var values = valueGenerator.Generate(emulator, emulator.Tags, now);
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
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
