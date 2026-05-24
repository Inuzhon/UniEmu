using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Common;
using UniEmu.Mapping;

namespace UniEmu.Features.CncPrograms;

/// <summary>
/// Выполняет прикладные операции с CNC-программами.
/// </summary>
public sealed class CncProgramService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    ScopedResourceValidator scopedResourceValidator)
{
    /// <summary>
    /// Возвращает CNC-программы с учетом области видимости и эмулятора.
    /// </summary>
    /// <param name="scope">Область видимости для фильтрации.</param>
    /// <param name="emulatorId">Идентификатор эмулятора для фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список CNC-программ.</returns>
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

    /// <summary>
    /// Создает CNC-программу в области видимости конкретного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Параметры создаваемой программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная программа или <see langword="null"/>, если эмулятор не найден.</returns>
    public Task<CncProgramDto?> CreateForEmulatorAsync(string emulatorId, CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        return CreateAsync(request with { Scope = CncScope.Emulator, EmulatorId = emulatorId }, cancellationToken);
    }

    /// <summary>
    /// Создает CNC-программу.
    /// </summary>
    /// <param name="request">Параметры создаваемой программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная программа или <see langword="null"/>, если область видимости некорректна.</returns>
    public async Task<CncProgramDto?> CreateAsync(CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        if (!await scopedResourceValidator.IsValidCncScopeAsync(request.Scope, request.EmulatorId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(request.Scope, request.EmulatorId, name, excludedProgramId: null, cancellationToken);

        var entity = new CncProgramEntity
        {
            Id = $"cnc-{Guid.NewGuid():N}"[..13],
            Name = name,
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
        dataCache.InvalidateCncPrograms();
        return entity.ToDto();
    }

    /// <summary>
    /// Обновляет метаданные и/или содержимое CNC-программы.
    /// </summary>
    /// <param name="programId">Идентификатор программы.</param>
    /// <param name="request">Новые значения программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Обновленная программа или <see langword="null"/>, если она не найдена.</returns>
    public async Task<CncProgramDto?> PatchAsync(string programId, PatchCncProgramRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.CncPrograms.FirstOrDefaultAsync(p => p.Id == programId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var nextName = entity.Name;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            nextName = request.Name.Trim();
            await EnsureUniqueNameAsync(
                UniEmuJson.EnumValue<CncScope>(entity.Scope),
                entity.EmulatorId,
                nextName,
                entity.Id,
                cancellationToken);
        }

        entity.Name = nextName;

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
        dataCache.InvalidateCncPrograms();
        return entity.ToDto();
    }

    /// <summary>
    /// Удаляет CNC-программу.
    /// </summary>
    /// <param name="programId">Идентификатор программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><see langword="true"/>, если программа была удалена.</returns>
    public async Task<bool> DeleteAsync(string programId, CancellationToken cancellationToken)
    {
        var deleted = await db.CncPrograms.Where(p => p.Id == programId).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            dataCache.InvalidateCncPrograms();
        }

        return deleted > 0;
    }

    private async Task EnsureUniqueNameAsync(
        CncScope scope,
        string? emulatorId,
        string name,
        string? excludedProgramId,
        CancellationToken cancellationToken)
    {
        var scopeValue = UniEmuJson.EnumString(scope);
        var query = db.CncPrograms
            .AsNoTracking()
            .Where(program => program.Scope == scopeValue && (excludedProgramId == null || program.Id != excludedProgramId));

        query = scope == CncScope.Shared
            ? query.Where(program => program.EmulatorId == null)
            : query.Where(program => program.EmulatorId == emulatorId);

        var existingNames = await query
            .Select(program => program.Name)
            .ToListAsync(cancellationToken);

        if (existingNames.Any(existingName => string.Equals(existingName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("CNC-программа с таким именем уже существует в этой области видимости.");
        }
    }

}
