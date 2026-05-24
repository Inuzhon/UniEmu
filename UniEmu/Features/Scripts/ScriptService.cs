using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Common;
using UniEmu.Mapping;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Features.Scripts;

/// <summary>
/// Выполняет прикладные операции с CSX-скриптами тегов.
/// </summary>
public sealed class ScriptService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    ScopedResourceValidator scopedResourceValidator,
    CsxLanguageService language,
    CompiledTagScriptCache compiledScripts)
{
    /// <summary>
    /// Возвращает скрипты с учетом области видимости и эмулятора.
    /// </summary>
    /// <param name="scope">Область видимости для фильтрации.</param>
    /// <param name="emulatorId">Идентификатор эмулятора для фильтрации.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список скриптов.</returns>
    public async Task<IReadOnlyList<ScriptFileDto>> ListAsync(ScriptScope? scope, string? emulatorId, CancellationToken cancellationToken)
    {
        var query = db.ScriptFiles.AsNoTracking();

        if (scope is not null)
        {
            var scopeValue = UniEmuJson.EnumString(scope.Value);
            query = query.Where(s => s.Scope == scopeValue);
        }

        if (!string.IsNullOrWhiteSpace(emulatorId))
        {
            query = query.Where(s => s.EmulatorId == emulatorId);
        }

        var scripts = await query.OrderBy(s => s.Scope).ThenBy(s => s.Name).ToListAsync(cancellationToken);
        return scripts.Select(s => s.ToDto()).ToList();
    }

    /// <summary>
    /// Создает новый CSX-скрипт с шаблонным содержимым.
    /// </summary>
    /// <param name="request">Параметры создаваемого скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданный скрипт или <see langword="null"/>, если область видимости некорректна.</returns>
    public async Task<ScriptFileDto?> CreateAsync(CreateScriptRequest request, CancellationToken cancellationToken)
    {
        if (!await scopedResourceValidator.IsValidScriptScopeAsync(request.Scope, request.EmulatorId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var name = request.Name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)
            ? request.Name.Trim()
            : $"{request.Name.Trim()}.csx";
        await EnsureUniqueNameAsync(request.Scope, request.EmulatorId, name, excludedScriptId: null, cancellationToken);

        var content = $"// {name}{Environment.NewLine}{Environment.NewLine}return 0;{Environment.NewLine}";
        var entity = new ScriptFileEntity
        {
            Id = $"scr-{Guid.NewGuid():N}"[..13],
            Name = name,
            Scope = UniEmuJson.EnumString(request.Scope),
            EmulatorId = request.Scope == ScriptScope.Emulator ? request.EmulatorId : null,
            Content = content,
            UpdatedAt = now,
            SizeBytes = content.Length,
        };

        db.ScriptFiles.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateScripts();
        compiledScripts.Clear();
        return entity.ToDto();
    }

    /// <summary>
    /// Обновляет имя и/или содержимое скрипта с проверкой CSX-кода.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <param name="request">Новые значения скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Обновленный скрипт или <see langword="null"/>, если он не найден.</returns>
    public async Task<ScriptFileDto?> PatchAsync(string scriptId, PatchScriptRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.ScriptFiles.FirstOrDefaultAsync(s => s.Id == scriptId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var nextName = entity.Name;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            nextName = request.Name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)
                ? request.Name.Trim()
                : $"{request.Name.Trim()}.csx";
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            await EnsureUniqueNameAsync(
                UniEmuJson.EnumValue<ScriptScope>(entity.Scope),
                entity.EmulatorId,
                nextName,
                entity.Id,
                cancellationToken);
        }

        if (request.Content is not null)
        {
            await ValidateContentAsync(entity, nextName, request.Content, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = nextName;
        }

        if (request.Content is not null)
        {
            entity.Content = request.Content;
            entity.SizeBytes = request.Content.Length;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateScripts();
        compiledScripts.Clear();
        return entity.ToDto();
    }

    /// <summary>
    /// Удаляет CSX-скрипт и очищает кэш скомпилированных скриптов.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><see langword="true"/>, если скрипт был удален.</returns>
    public async Task<bool> DeleteAsync(string scriptId, CancellationToken cancellationToken)
    {
        var deleted = await db.ScriptFiles.Where(s => s.Id == scriptId).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            dataCache.InvalidateScripts();
            compiledScripts.Clear();
        }

        return deleted > 0;
    }

    private async Task EnsureUniqueNameAsync(
        ScriptScope scope,
        string? emulatorId,
        string name,
        string? excludedScriptId,
        CancellationToken cancellationToken)
    {
        var scopeValue = UniEmuJson.EnumString(scope);
        var query = db.ScriptFiles
            .AsNoTracking()
            .Where(script => script.Scope == scopeValue && (excludedScriptId == null || script.Id != excludedScriptId));

        query = scope == ScriptScope.Shared
            ? query.Where(script => script.EmulatorId == null)
            : query.Where(script => script.EmulatorId == emulatorId);

        var normalizedName = TagScriptPath.Normalize(name);
        var existingNames = await query
            .Select(script => script.Name)
            .ToListAsync(cancellationToken);

        if (existingNames.Any(existingName =>
                string.Equals(TagScriptPath.Normalize(existingName), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Скрипт с таким именем уже существует в этой области видимости.");
        }
    }

    private async Task ValidateContentAsync(
        ScriptFileEntity entity,
        string nextName,
        string nextContent,
        CancellationToken cancellationToken)
    {
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
        var visibleScriptEntities = await db.ScriptFiles
            .AsNoTracking()
            .Where(script => script.Id != entity.Id && (script.Scope == sharedScope || script.EmulatorId == entity.EmulatorId))
            .ToListAsync(cancellationToken);

        var visibleScripts = VisibleScriptResolver.ToContentMap(visibleScriptEntities);
        VisibleScriptResolver.AddOrReplace(visibleScripts, nextName, nextContent);

        var result = await language.AnalyzeAsync(nextName, nextContent, visibleScripts, typeof(TagScriptGlobals), cancellationToken);
        var errors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new CsxScriptValidationException(errors);
        }
    }
}
