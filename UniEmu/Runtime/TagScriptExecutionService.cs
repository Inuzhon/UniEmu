using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Hosting;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime;

public sealed class TagScriptExecutionService
{
    public TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        TagPreviewFlushService? previewFlushService = null)
        : this(
            db,
            dataCache,
            stateStore,
            scriptCache,
            new CsxScriptEnvironment(),
            new CsxScriptDirectiveValidator(),
            new CsxScriptSecurityValidator(),
            previewFlushService)
    {
    }

    public TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        CsxScriptEnvironment scriptEnvironment,
        CsxScriptDirectiveValidator directiveValidator,
        CsxScriptSecurityValidator securityValidator,
        TagPreviewFlushService? previewFlushService = null)
        : this(db, dataCache, stateStore, scriptCache, scriptEnvironment, directiveValidator, securityValidator, null, previewFlushService)
    {
    }

    internal TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        ITagScriptRestOperations? restOperations,
        TagPreviewFlushService? previewFlushService = null)
        : this(
            db,
            dataCache,
            stateStore,
            scriptCache,
            new CsxScriptEnvironment(),
            new CsxScriptDirectiveValidator(),
            new CsxScriptSecurityValidator(),
            restOperations,
            previewFlushService)
    {
    }

    internal TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        CsxScriptEnvironment scriptEnvironment,
        CsxScriptDirectiveValidator directiveValidator,
        CsxScriptSecurityValidator securityValidator,
        ITagScriptRestOperations? restOperations,
        TagPreviewFlushService? previewFlushService = null)
    {
        this.db = db;
        this.dataCache = dataCache;
        this.stateStore = stateStore;
        this.scriptCache = scriptCache;
        this.scriptEnvironment = scriptEnvironment;
        this.directiveValidator = directiveValidator;
        this.securityValidator = securityValidator;
        this.restOperations = restOperations;
        this.previewFlushService = previewFlushService;
    }

    private readonly UniEmuDbContext db;
    private readonly CachedUniEmuDataService dataCache;
    private readonly TagRuntimeStateStore stateStore;
    private readonly CompiledTagScriptCache scriptCache;
    private readonly CsxScriptEnvironment scriptEnvironment;
    private readonly CsxScriptDirectiveValidator directiveValidator;
    private readonly CsxScriptSecurityValidator securityValidator;
    private readonly ITagScriptRestOperations? restOperations;
    private readonly TagPreviewFlushService? previewFlushService;

    public async Task<GeneratedTagValue> GenerateScriptTagAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        object? currentValue = null)
    {
        var script = await ResolveEntryScriptAsync(emulator.Id, tag, cancellationToken);
        var entryContent = TagScriptContentNormalizer.NormalizeEntryScriptContent(script.Content);
        directiveValidator.ValidateSupportedDirectives(entryContent);

        var scripts = await LoadVisibleScriptsAsync(emulator.Id, cancellationToken);
        scripts[script.Path] = entryContent;
        directiveValidator.DetectLoadCycles(script.Path, scripts);

        var state = await GetOrCreateStateAsync(emulator.Id, script.StateKey, cancellationToken);
        var stateValues = UniEmuJson.Deserialize<Dictionary<string, object?>>(state.ValuesJson)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var globals = BuildGlobals(emulator, tag, timestamp, stateValues, cancellationToken, currentValue);
        var scriptOptions = scriptEnvironment.CreateScriptOptions(script.Path, scripts, typeof(TagScriptGlobals));
        ValidateSecurity(script.Path, entryContent, scripts, scriptOptions, typeof(TagScriptGlobals), cancellationToken);
        var compiledScript = scriptCache.GetOrAdd(script.Path, entryContent, scripts, scriptOptions, typeof(TagScriptGlobals));
        var scriptState = await compiledScript.RunAsync(globals, cancellationToken);
        var result = scriptState.ReturnValue;

        if (globals.UniEmu.State.IsDirty || globals.UniEmu.Tags.IsDirty)
        {
            state.ValuesJson = UniEmuJson.Serialize(globals.UniEmu.State.Snapshot());
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = TelemetryValueGenerator.ApplyTagRounding(tagType, tag, CastResult(tagType, result, tag.Preview));
        SpecialParameter? specialParameter = string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);

        return new GeneratedTagValue(tag.Key, tag.Name, value, TelemetryValueGenerator.ToNumericValue(value), specialParameter);
    }

    private async Task<ScriptContent> ResolveEntryScriptAsync(
        string emulatorId,
        EmulatorTagEntity tag,
        CancellationToken cancellationToken)
    {
        var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);

        if (!string.IsNullOrWhiteSpace(formula?.InlineScript))
            return new ScriptContent($"inline/{tag.Id}.csx", formula.InlineScript, $"inline:{tag.Id}");

        if (string.IsNullOrWhiteSpace(formula?.ScriptId))
            return new ScriptContent($"inline/{tag.Id}.csx", "return null;", $"inline:{tag.Id}");

        var visibleScripts = await dataCache.GetVisibleScriptsAsync(emulatorId, cancellationToken);
        var script = visibleScripts.FirstOrDefault(s => s.Id == formula.ScriptId);

        if (script is null)
            throw new InvalidOperationException($"Script '{formula.ScriptId}' was not found for tag '{tag.Name}'.");

        return new ScriptContent(TagScriptPath.Normalize(script.Name), script.Content, $"script:{script.Id}");
    }

    private async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var scripts = await dataCache.GetVisibleScriptsAsync(emulatorId, cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts)
        {
            var path = TagScriptPath.Normalize(script.Name);
            directiveValidator.ValidateSupportedDirectives(script.Content);
            result[path] = script.Content;
        }

        return result;
    }

    private void ValidateSecurity(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions options,
        Type globalsType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var script = CSharpScript.Create<object?>(
            content,
            options
                .WithFilePath(TagScriptPath.Normalize(entryPath))
                .WithSourceResolver(new DbScriptSourceResolver(visibleScripts)),
            globalsType);

        _ = script.Compile(cancellationToken);

        var diagnostics = securityValidator.Validate(script.GetCompilation());
        if (diagnostics.Count > 0)
        {
            throw new CsxScriptValidationException(diagnostics);
        }
    }

    private async Task<ScriptRuntimeStateEntity> GetOrCreateStateAsync(
        string emulatorId,
        string scriptKey,
        CancellationToken cancellationToken)
    {
        var state = await db.ScriptRuntimeStates
            .FirstOrDefaultAsync(s => s.EmulatorId == emulatorId && s.ScriptKey == scriptKey, cancellationToken);

        if (state is not null)
            return state;

        state = new ScriptRuntimeStateEntity
        {
            Id = $"srs-{Guid.NewGuid():N}"[..13],
            EmulatorId = emulatorId,
            ScriptKey = scriptKey,
            ValuesJson = "{}",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.ScriptRuntimeStates.Add(state);

        return state;
    }

    private TagScriptGlobals BuildGlobals(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        Dictionary<string, object?> stateValues,
        CancellationToken cancellationToken,
        object? currentValue)
    {
        var values = emulator.Tags
            .Select(t =>
            {
                var tagType = UniEmuJson.EnumValue<TagType>(t.Type);
                var scriptTagType = ToScriptValueType(tagType);
                if (t.Id == tag.Id && currentValue is not null)
                    return new TagScriptValue(t.Key, t.Name, currentValue, scriptTagType, timestamp);

                if (stateStore.TryGet(emulator.Id, t.Id, out var runtimeValue))
                    return new TagScriptValue(t.Key, t.Name, runtimeValue.Value, scriptTagType, runtimeValue.Timestamp);

                return new TagScriptValue(t.Key, t.Name, ConvertPreview(t), scriptTagType, timestamp);
            })
            .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.Timestamp).First(), StringComparer.OrdinalIgnoreCase);

        stateStore.TryGet(emulator.Id, tag.Id, out var previous);
        var scriptNow = ApplicationGlobalization.ToApplicationTime(timestamp);

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var tagValue = currentValue ?? previous?.Value;
        var tagTimestamp = currentValue is null ? previous?.Timestamp : timestamp;
        return new TagScriptGlobals(
            scriptNow,
            new TagScriptValue(tag.Key, tag.Name, tagValue, ToScriptValueType(tagType), tagTimestamp),
            new TagScriptTagAccessor(
                values,
                (tagName, value) => SetStaticTag(emulator, tagName, value, timestamp)),
            new TagScriptEmulatorContext(emulator.Id, emulator.Name, emulator.Status, emulator.StartedAt),
            new TagScriptStateContext(
                emulator.Status == nameof(EmulatorStatus.Running),
                previous?.Value,
                previous?.NumericValue,
                previous?.Timestamp,
                ToScriptStateValues(stateValues)
            ),
            restOperations is null
                ? TagScriptRestContext.CreateDisabled(cancellationToken)
                : new TagScriptRestContext(restOperations, cancellationToken)
        );
    }

    private static Dictionary<string, TagScriptValue> ToScriptStateValues(Dictionary<string, object?> stateValues)
    {
        return stateValues.ToDictionary(
            value => value.Key,
            value => new TagScriptValue(value.Key, value.Key, value.Value, ToScriptValueType(value.Value), null),
            StringComparer.OrdinalIgnoreCase);
    }

    private static TagScriptValueType ToScriptValueType(object? value) => value switch
    {
        bool => TagScriptValueType.Bool,
        byte or short or int or long => TagScriptValueType.Int,
        float or double or decimal => TagScriptValueType.Double,
        _ => TagScriptValueType.String,
    };

    private static TagScriptValueType ToScriptValueType(TagType type) => type switch
    {
        TagType.Bool => TagScriptValueType.Bool,
        TagType.Int => TagScriptValueType.Int,
        TagType.Double => TagScriptValueType.Double,
        TagType.String => TagScriptValueType.String,
        _ => TagScriptValueType.String,
    };

    private object? SetStaticTag(EmulatorEntity emulator, string tagName, object? value, DateTimeOffset timestamp)
    {
        var tag = emulator.Tags.FirstOrDefault(t =>
            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) ||
            t.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase)
        );

        if (tag is null)
            throw new InvalidOperationException($"Tag '{tagName}' was not found.");

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source != TagSource.Static)
            throw new InvalidOperationException($"Tag '{tagName}' is not static.");

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var typedValue = TelemetryValueGenerator.ApplyTagRounding(tagType, tag, CastResult(tagType, value, tag.Preview));

        tag.Preview = TelemetryValueGenerator.ToPreview(typedValue);
        stateStore.Set(emulator.Id, tag.Id, tag.Name, typedValue, TelemetryValueGenerator.ToNumericValue(typedValue), timestamp);
        previewFlushService?.MarkDirty(emulator.Id, tag.Id, tag.Preview);

        return typedValue;
    }

    private static object? CastResult(TagType tagType, object? result, string preview)
    {
        if (result is null)
            return tagType == TagType.String ? preview : CastPreview(tagType, preview);

        return tagType switch
        {
            TagType.Bool => ToBool(result, preview),
            TagType.Int => (int)Math.Round(ToDouble(result, preview)),
            TagType.Double => ToDouble(result, preview),
            TagType.String => TelemetryValueGenerator.ToPreview(result),
            _ => result,
        };
    }

    private static object? CastPreview(TagType tagType, string preview) => tagType switch
    {
        TagType.Bool => ToBool(preview, "false"),
        TagType.Int => (int)Math.Round(ToDouble(preview, "0")),
        TagType.Double => ToDouble(preview, "0"),
        TagType.String => preview,
        _ => null,
    };

    private static object? ConvertPreview(EmulatorTagEntity tag)
    {
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        return CastPreview(tagType, tag.Preview);
    }

    private static bool ToBool(object value, string preview) => value switch
    {
        bool boolValue => boolValue,
        string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
        string stringValue => ToDouble(stringValue, preview) != 0,
        _ => ToDouble(value, preview) != 0,
    };

    private static double ToDouble(object value, string preview) => value switch
    {
        byte byteValue => byteValue,
        short shortValue => shortValue,
        int intValue => intValue,
        long longValue => longValue,
        float floatValue => floatValue,
        double doubleValue => doubleValue,
        decimal decimalValue => (double)decimalValue,
        bool boolValue => boolValue ? 1 : 0,
        string stringValue => double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : (double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0),
        IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
        _ => double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0,
    };

    private sealed record ScriptContent(string Path, string Content, string StateKey);
}
