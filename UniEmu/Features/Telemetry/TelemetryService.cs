using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;

namespace UniEmu.Features.Telemetry;

public sealed class TelemetryService(UniEmuDbContext db)
{
    public async Task<IReadOnlyList<TelemetryPointDto>?> GetAsync(string emulatorId, int points, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        var take = Math.Clamp(points <= 0 ? 60 : points, 1, 1000);
        var telemetry = await db.TelemetryPoints
            .AsNoTracking()
            .Where(t => t.EmulatorId == emulatorId)
            .ToListAsync(cancellationToken);

        return telemetry
            .OrderByDescending(t => t.Timestamp)
            .Take(take)
            .OrderBy(t => t.Timestamp)
            .Select(t => t.ToDto())
            .ToList();
    }

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
        return entity.ToDto();
    }
}
