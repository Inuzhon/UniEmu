using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Features.Emulators;

public sealed class EmulatorService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    EmulatorScheduleService scheduleService,
    RuntimeUpdateService runtimeUpdateService)
{
    public async Task<IReadOnlyList<EmulatorDto>> ListAsync(CancellationToken cancellationToken)
    {
        var emulators = await db.Emulators
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new
            {
                Entity = e,
                TagsCount = e.Tags.Count,
            })
            .ToListAsync(cancellationToken);

        return emulators.Select(e => e.Entity.ToDto(e.TagsCount)).ToList();
    }

    public async Task<EmulatorDto?> GetAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var emulator = await db.Emulators
            .AsNoTracking()
            .Where(e => e.Id == emulatorId)
            .Select(e => new
            {
                Entity = e,
                TagsCount = e.Tags.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return emulator?.Entity.ToDto(emulator.TagsCount);
    }

    public async Task<EmulatorDto> CreateAsync(CreateEmulatorRequest request, CancellationToken cancellationToken)
    {
        var entity = new EmulatorEntity
        {
            Id = $"em-{Guid.NewGuid():N}"[..12],
            Name = request.Name.Trim(),
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = request.ProtocolId,
            TargetUrl = request.TargetUrl.Trim(),
            IntervalSec = Math.Max(1, request.IntervalSec),
        };

        db.Emulators.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);

        var dto = entity.ToDto(tagsCount: 0);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<EmulatorDto?> PatchAsync(string emulatorId, PatchEmulatorRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var shouldReschedule = false;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            entity.TargetUrl = request.TargetUrl.Trim();
        }

        if (request.ProtocolId is not null)
        {
            entity.ProtocolId = request.ProtocolId.Value;
        }

        if (request.IntervalSec is not null)
        {
            entity.IntervalSec = Math.Max(1, request.IntervalSec.Value);
            if (entity.Status == nameof(EmulatorStatus.Running))
            {
                entity.NextRun = DateTimeOffset.UtcNow.AddSeconds(entity.IntervalSec);
                shouldReschedule = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);
        if (shouldReschedule)
        {
            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        var dto = entity.ToDto(entity.Tags.Count);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<EmulatorDto?> PatchStatusAsync(string emulatorId, PatchEmulatorStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var previousStatus = entity.Status;
        entity.Status = request.Status.ToString();
        var wasRunning = previousStatus == nameof(EmulatorStatus.Running);
        var isRunning = request.Status == EmulatorStatus.Running;

        if (isRunning)
        {
            var now = DateTimeOffset.UtcNow;
            entity.StartedAt ??= now;
            entity.NextRun = now;
            entity.LastError = null;
        }
        else if (request.Status == EmulatorStatus.Stopped)
        {
            entity.StartedAt = null;
            entity.NextRun = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);
        if (isRunning)
        {
            if (!wasRunning)
            {
                await scheduleService.ExecuteEventTagsAsync(entity.Id, TagTriggerEvent.OnStart, cancellationToken);
            }

            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }
        else
        {
            if (wasRunning && request.Status == EmulatorStatus.Stopped)
            {
                await scheduleService.ExecuteEventTagsAsync(entity.Id, TagTriggerEvent.OnStop, cancellationToken);
            }

            await scheduleService.UnscheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        var dto = entity.ToDto(entity.Tags.Count);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<bool> DeleteAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var deleted = await db.Emulators
            .Where(e => e.Id == emulatorId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return false;
        }

        await scheduleService.UnscheduleEmulatorAsync(emulatorId, cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        dataCache.InvalidateScripts();
        dataCache.InvalidateCncPrograms();
        return true;
    }
}
