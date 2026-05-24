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
        var events = cursor is null
            ? await db.SystemEvents
                .FromSqlInterpolated($"""
                    SELECT "Id", "EmulatorId", "EmulatorName", "Level", "Message", "Timestamp"
                    FROM "SystemEvents"
                    ORDER BY "Timestamp" DESC
                    LIMIT {take}
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
            : await db.SystemEvents
                .FromSqlInterpolated($"""
                    SELECT "Id", "EmulatorId", "EmulatorName", "Level", "Message", "Timestamp"
                    FROM "SystemEvents"
                    WHERE "Timestamp" < {cursor.Value}
                    ORDER BY "Timestamp" DESC
                    LIMIT {take}
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        return events
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.ToDto())
            .ToList();
    }

    /// <summary>
    /// Создает системное событие и публикует realtime-уведомление.
    /// </summary>
    /// <param name="request">Данные создаваемого события.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданное событие или <see langword="null"/>, если эмулятор не найден.</returns>
    public async Task<SystemEventDto?> CreateAsync(PushEventRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == request.EmulatorId, cancellationToken))
        {
            return null;
        }

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
