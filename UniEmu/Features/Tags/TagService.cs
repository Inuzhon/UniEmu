using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Features.Tags;

public sealed class TagService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    EmulatorScheduleService scheduleService,
    CsxLanguageService language)
{
    public async Task<IReadOnlyList<EmulatorTagDto>?> ListAsync(string emulatorId, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        var tags = await db.EmulatorTags
            .AsNoTracking()
            .Where(t => t.EmulatorId == emulatorId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(t => t.ToDto()).ToList();
    }

    public async Task<EmulatorTagDto?> CreateAsync(string emulatorId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        await ValidateInlineScriptAsync(emulatorId, $"inline/{request.Name.Trim()}.csx", request.Formula?.InlineScript, request.Type, cancellationToken);

        var entity = new EmulatorTagEntity
        {
            Id = $"tg-{Guid.NewGuid():N}"[..12],
            EmulatorId = emulatorId,
            Name = request.Name.Trim(),
            Key = request.Key.Trim(),
            Type = UniEmuJson.EnumString(request.Type),
            Source = UniEmuJson.EnumString(request.Source),
            Preview = request.Preview,
            TriggerJson = UniEmuJson.Serialize(request.Trigger),
            CalcJson = request.Calc is null ? null : UniEmuJson.Serialize(request.Calc),
            FormulaJson = request.Formula is null ? null : UniEmuJson.Serialize(request.Formula),
            ScenarioJson = request.Scenario is null ? null : UniEmuJson.Serialize(request.Scenario),
            Enabled = request.Enabled ?? true,
            RoundDigits = NormalizeRoundDigits(request.RoundDigits),
            SpecialParameter = request.SpecialParameter is null ? null : UniEmuJson.EnumString(request.SpecialParameter.Value),
            Description = request.Description,
        };

        db.EmulatorTags.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmulatorTagDto?> ReplaceAsync(string emulatorId, string tagId, ReplaceTagRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.EmulatorTags
            .FirstOrDefaultAsync(t => t.EmulatorId == emulatorId && t.Id == tagId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        await ValidateInlineScriptAsync(emulatorId, $"inline/{entity.Id}.csx", request.Formula?.InlineScript, request.Type, cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Key = request.Key.Trim();
        entity.Type = UniEmuJson.EnumString(request.Type);
        entity.Source = UniEmuJson.EnumString(request.Source);
        entity.Preview = request.Preview;
        entity.TriggerJson = UniEmuJson.Serialize(request.Trigger);
        entity.CalcJson = request.Calc is null ? null : UniEmuJson.Serialize(request.Calc);
        entity.FormulaJson = request.Formula is null ? null : UniEmuJson.Serialize(request.Formula);
        entity.ScenarioJson = request.Scenario is null ? null : UniEmuJson.Serialize(request.Scenario);
        entity.Enabled = request.Enabled ?? true;
        entity.RoundDigits = NormalizeRoundDigits(request.RoundDigits);
        entity.SpecialParameter = request.SpecialParameter is null ? null : UniEmuJson.EnumString(request.SpecialParameter.Value);
        entity.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(string emulatorId, string tagId, CancellationToken cancellationToken)
    {
        var deleted = await db.EmulatorTags
            .Where(t => t.EmulatorId == emulatorId && t.Id == tagId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            dataCache.InvalidateEmulator(emulatorId);
            await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        }

        return deleted > 0;
    }

    private static int? NormalizeRoundDigits(int? value)
    {
        return value is null ? null : Math.Clamp(value.Value, 0, 15);
    }

    private async Task ValidateInlineScriptAsync(
        string emulatorId,
        string entryPath,
        string? inlineScript,
        TagType tagType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inlineScript))
        {
            return;
        }

        var visibleScripts = (await dataCache.GetVisibleScriptsAsync(emulatorId, cancellationToken))
            .ToDictionary(script => TagScriptPath.Normalize(script.Name), script => script.Content, StringComparer.OrdinalIgnoreCase);

        var result = await language.AnalyzeAsync(
            entryPath,
            inlineScript,
            visibleScripts,
            typeof(TagScriptGlobals),
            ExpectedScriptReturnType(tagType),
            cancellationToken);
        var errors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new CsxScriptValidationException(errors);
        }
    }

    private static Type ExpectedScriptReturnType(TagType tagType) => tagType switch
    {
        TagType.Bool => typeof(bool),
        TagType.Int => typeof(int),
        TagType.Double => typeof(double),
        TagType.String => typeof(string),
        _ => typeof(object),
    };
}
