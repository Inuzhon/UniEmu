using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.Events;

public sealed class EventService(UniEmuDbContext db)
{
    public async Task<IReadOnlyList<SystemEventDto>> ListAsync(DateTimeOffset? cursor, int limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var events = await db.SystemEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return events
            .Where(e => cursor is null || e.Timestamp < cursor)
            .OrderByDescending(e => e.Timestamp)
            .Take(take)
            .Select(e => e.ToDto())
            .ToList();
    }

    public async Task<SystemEventDto> CreateAsync(PushEventRequest request, CancellationToken cancellationToken)
    {
        var entity = new SystemEventEntity
        {
            Id = $"ev-{Guid.NewGuid():N}"[..12],
            EmulatorId = request.EmulatorId,
            EmulatorName = request.EmulatorName,
            Level = UniEmuJson.EnumString(request.Level),
            Message = request.Message,
            Timestamp = request.Timestamp == default ? DateTimeOffset.UtcNow : request.Timestamp,
        };

        db.SystemEvents.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}
