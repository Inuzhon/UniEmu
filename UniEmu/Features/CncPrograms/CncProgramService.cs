using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.CncPrograms;

public sealed class CncProgramService(UniEmuDbContext db)
{
    public async Task<IReadOnlyList<CncProgramDto>> ListAsync(CncScope? scope, string? emulatorId, CancellationToken cancellationToken)
    {
        var query = db.CncPrograms.AsNoTracking();

        if (scope is not null)
        {
            var scopeValue = UniEmuJson.EnumString(scope.Value);
            query = query.Where(p => p.Scope == scopeValue);
        }

        if (!string.IsNullOrWhiteSpace(emulatorId))
        {
            query = query.Where(p => p.EmulatorId == emulatorId);
        }

        var programs = await query.OrderBy(p => p.Scope).ThenBy(p => p.Name).ToListAsync(cancellationToken);
        return programs.Select(p => p.ToDto()).ToList();
    }

    public Task<CncProgramDto?> CreateForEmulatorAsync(string emulatorId, CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        return CreateAsync(request with { Scope = CncScope.Emulator, EmulatorId = emulatorId }, cancellationToken);
    }

    public async Task<CncProgramDto?> CreateAsync(CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        if (!await IsScopeValidAsync(request.Scope, request.EmulatorId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new CncProgramEntity
        {
            Id = $"cnc-{Guid.NewGuid():N}"[..13],
            Name = request.Name.Trim(),
            Scope = UniEmuJson.EnumString(request.Scope),
            EmulatorId = request.Scope == CncScope.Emulator ? request.EmulatorId : null,
            Description = request.Description ?? string.Empty,
            Content = request.Content,
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : request.Content.Length,
            IsBinary = request.IsBinary,
            UpdatedAt = now,
            UploadedAt = now,
        };

        db.CncPrograms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<CncProgramDto?> PatchAsync(string programId, PatchCncProgramRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.CncPrograms.FirstOrDefaultAsync(p => p.Id == programId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            entity.Description = request.Description;
        }

        if (request.Content is not null)
        {
            entity.Content = request.Content;
            entity.SizeBytes = request.Content.Length;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(string programId, CancellationToken cancellationToken)
    {
        return await db.CncPrograms.Where(p => p.Id == programId).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private async Task<bool> IsScopeValidAsync(CncScope scope, string? emulatorId, CancellationToken cancellationToken)
    {
        return scope switch
        {
            CncScope.Shared => string.IsNullOrWhiteSpace(emulatorId),
            CncScope.Emulator => !string.IsNullOrWhiteSpace(emulatorId)
                && await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken),
            _ => false,
        };
    }
}
