using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Features.Events;

/// <summary>
/// Выполняет прикладные операции с системными событиями.
/// </summary>
public sealed class EventService(UniEmuDbContext db, RuntimeUpdateService runtimeUpdateService)
{
    /// <summary>
    /// Возвращает страницу системных событий.
    /// </summary>
    /// <param name="cursor">Временная метка, старше которой нужно вернуть события.</param>
    /// <param name="limit">Максимальное количество событий.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список событий, отсортированный от новых к старым.</returns>
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

    /// <summary>
    /// Создает системное событие и публикует realtime-уведомление.
    /// </summary>
    /// <param name="request">Данные создаваемого события.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданное событие.</returns>
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
        var dto = entity.ToDto();
        await runtimeUpdateService.PublishEventCreatedAsync(dto, cancellationToken);

        return dto;
    }
}
