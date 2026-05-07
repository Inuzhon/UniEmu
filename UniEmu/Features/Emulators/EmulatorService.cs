using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Features.Contracts;
using UniEmu.Runtime;

namespace UniEmu.Features.Emulators;

public sealed class EmulatorService(UniEmuDbContext db, EmulatorScheduleService scheduleService)
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
            TargetUrl = request.TargetUrl.Trim(),
            IntervalSec = Math.Max(1, request.IntervalSec),
        };

        db.Emulators.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity.ToDto(tagsCount: 0);
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
        if (shouldReschedule)
        {
            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        return entity.ToDto(entity.Tags.Count);
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

        entity.Status = request.Status.ToString();

        if (request.Status == EmulatorStatus.Running)
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
        if (request.Status == EmulatorStatus.Running)
        {
            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }
        else
        {
            await scheduleService.UnscheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        return entity.ToDto(entity.Tags.Count);
    }
}
