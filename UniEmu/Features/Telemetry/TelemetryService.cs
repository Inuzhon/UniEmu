using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Features.Telemetry;

/// <summary>
/// Выполняет прикладные операции с телеметрией эмуляторов.
/// </summary>
public sealed class TelemetryService(UniEmuDbContext db, RuntimeUpdateService runtimeUpdateService)
{
    /// <summary>
    /// Возвращает последние точки телеметрии эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="points">Запрошенное количество точек.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список точек телеметрии или <see langword="null"/>, если эмулятор не найден.</returns>
    public async Task<IReadOnlyList<TelemetryPointDto>?> GetAsync(string emulatorId, int points, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        var take = Math.Clamp(points <= 0 ? 60 : points, 1, 1000);
        var telemetry = await db.TelemetryPoints
            .FromSqlInterpolated($"""
                SELECT "Id", "EmulatorId", "Timestamp", "ValuesJson"
                FROM "TelemetryPoints"
                WHERE "EmulatorId" = {emulatorId}
                ORDER BY "Timestamp" DESC
                LIMIT {take}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return telemetry
            .OrderBy(t => t.Timestamp)
            .Select(t => t.ToDto())
            .ToList();
    }

    /// <summary>
    /// Записывает точку телеметрии и публикует realtime-уведомление.
    /// </summary>
    /// <param name="request">Данные точки телеметрии.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная точка телеметрии или <see langword="null"/>, если эмулятор не найден.</returns>
    public async Task<TelemetryPointDto?> IngestAsync(TelemetryIngestRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == request.EmulatorId, cancellationToken))
        {
            return null;
        }

        var entity = new TelemetryPointEntity
        {
            EmulatorId = request.EmulatorId,
            Timestamp = request.Timestamp == default ? DateTimeOffset.UtcNow : request.Timestamp,
            ValuesJson = UniEmuJson.Serialize(request.Values),
        };

        db.TelemetryPoints.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var dto = entity.ToDto();
        await runtimeUpdateService.PublishTelemetryAsync(request.EmulatorId, dto, cancellationToken);
        return dto;
    }
}
