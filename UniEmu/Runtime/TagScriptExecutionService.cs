using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;

namespace UniEmu.Runtime;

public sealed class TagScriptExecutionService(UniEmuDbContext db, TagRuntimeStateStore stateStore)
{
    private static readonly Regex BlockedDirective = new(
        @"^\s*#\s*(r|using)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex LoadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly ScriptOptions BaseOptions = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(DateTimeOffset).Assembly,
            typeof(UniEmuJson).Assembly)
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.Globalization",
            "System.Linq",
            "UniEmu.Runtime");

    public async Task<GeneratedTagValue> GenerateScriptTagAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var script = await ResolveEntryScriptAsync(emulator.Id, tag, cancellationToken);
        ValidateDirectives(script.Content);

        var scripts = await LoadVisibleScriptsAsync(emulator.Id, cancellationToken);
        scripts[script.Path] = script.Content;
        DetectLoadCycles(script.Path, scripts);

        var state = await GetOrCreateStateAsync(emulator.Id, script.StateKey, cancellationToken);
        var stateValues = UniEmuJson.Deserialize<Dictionary<string, object?>>(state.ValuesJson)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var globals = BuildGlobals(emulator, tag, timestamp, stateValues);
        var options = BaseOptions.WithSourceResolver(new DbScriptSourceResolver(scripts));
        var result = await CSharpScript.EvaluateAsync<object?>(
            script.Content,
            options,
            globals,
            typeof(TagScriptGlobals),
            cancellationToken);

        if (globals.State.IsDirty || globals.Tags.IsDirty)
        {
            state.ValuesJson = UniEmuJson.Serialize(globals.State.Snapshot());
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = CastResult(tagType, result, tag.Preview);
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
        {
            return new ScriptContent($"inline/{tag.Id}.csx", formula.InlineScript, $"inline:{tag.Id}");
        }

        if (string.IsNullOrWhiteSpace(formula?.ScriptId))
        {
            return new ScriptContent($"inline/{tag.Id}.csx", "return null;", $"inline:{tag.Id}");
        }

        var script = await db.ScriptFiles
            .AsNoTracking()
            .Where(s => s.Id == formula.ScriptId)
            .Where(s => s.EmulatorId == emulatorId || s.Scope == UniEmuJson.EnumString(ScriptScope.Shared))
            .FirstOrDefaultAsync(cancellationToken);

        if (script is null)
        {
            throw new InvalidOperationException($"Script '{formula.ScriptId}' was not found for tag '{tag.Name}'.");
        }

        return new ScriptContent(NormalizePath(script.Name), script.Content, $"script:{script.Id}");
    }

    private async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
        var scripts = await db.ScriptFiles
            .AsNoTracking()
            .Where(s => s.Scope == sharedScope || s.EmulatorId == emulatorId)
            .OrderBy(s => s.Scope == sharedScope ? 0 : 1)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts)
        {
            var path = NormalizePath(script.Name);
            ValidateDirectives(script.Content);
            result[path] = script.Content;
        }

        return result;
    }

    private async Task<ScriptRuntimeStateEntity> GetOrCreateStateAsync(
        string emulatorId,
        string scriptKey,
        CancellationToken cancellationToken)
    {
        var state = await db.ScriptRuntimeStates
            .FirstOrDefaultAsync(s => s.EmulatorId == emulatorId && s.ScriptKey == scriptKey, cancellationToken);

        if (state is not null)
        {
            return state;
        }

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
        Dictionary<string, object?> stateValues)
    {
        var values = emulator.Tags
            .Select(t =>
            {
                if (stateStore.TryGet(emulator.Id, t.Id, out var runtimeValue))
                {
                    return new KeyValuePair<string, object?>(t.Name, runtimeValue.Value);
                }

                return new KeyValuePair<string, object?>(t.Name, ConvertPreview(t));
            })
            .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        stateStore.TryGet(emulator.Id, tag.Id, out var previous);

        return new TagScriptGlobals(
            new TagScriptTagAccessor(
                values,
                (tagName, value) => SetStaticTag(emulator, tagName, value, timestamp)),
            timestamp,
            new TagScriptEmulatorContext(emulator.Id, emulator.Name, emulator.Status),
            new TagScriptStateContext(
                emulator.Status == nameof(EmulatorStatus.Running),
                previous?.Value,
                previous?.NumericValue,
                previous?.Timestamp,
                stateValues));
    }

    private object? SetStaticTag(EmulatorEntity emulator, string tagName, object? value, DateTimeOffset timestamp)
    {
        var tag = emulator.Tags.FirstOrDefault(t =>
            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)
            || t.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase));

        if (tag is null)
        {
            throw new InvalidOperationException($"Tag '{tagName}' was not found.");
        }

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source != TagSource.Static)
        {
            throw new InvalidOperationException($"Tag '{tagName}' is not static.");
        }

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var typedValue = CastResult(tagType, value, tag.Preview);
        tag.Preview = ToPreview(typedValue);
        stateStore.Set(emulator.Id, tag.Id, tag.Name, typedValue, TelemetryValueGenerator.ToNumericValue(typedValue), timestamp);
        return typedValue;
    }

    private static void ValidateDirectives(string content)
    {
        var match = BlockedDirective.Match(content);
        if (match.Success)
        {
            throw new InvalidOperationException($"Unsupported script directive '{match.Value.Trim()}'. Use #load for shared scripts.");
        }
    }

    private static void DetectLoadCycles(string entryPath, IReadOnlyDictionary<string, string> scripts)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(NormalizePath(entryPath), visited, stack, scripts);
    }

    private static void Visit(
        string path,
        HashSet<string> visited,
        HashSet<string> stack,
        IReadOnlyDictionary<string, string> scripts)
    {
        if (stack.Contains(path))
        {
            throw new InvalidOperationException($"Cyclic #load detected at '{path}'.");
        }

        if (!visited.Add(path) || !scripts.TryGetValue(path, out var content))
        {
            return;
        }

        stack.Add(path);
        foreach (Match match in LoadDirective.Matches(content))
        {
            var loadPath = ResolveLoadPath(match.Groups["path"].Value, path, scripts);
            if (loadPath is null)
            {
                continue;
            }

            Visit(loadPath, visited, stack, scripts);
        }

        stack.Remove(path);
    }

    private static string? ResolveLoadPath(string path, string baseFilePath, IReadOnlyDictionary<string, string> scripts)
    {
        var normalized = NormalizePath(path);
        if (scripts.ContainsKey(normalized))
        {
            return normalized;
        }

        var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var relative = NormalizePath($"{baseDir}/{path}");
        return scripts.ContainsKey(relative) ? relative : null;
    }

    private static object? CastResult(TagType tagType, object? result, string preview)
    {
        if (result is null)
        {
            return tagType == TagType.String ? preview : CastPreview(tagType, preview);
        }

        return tagType switch
        {
            TagType.Bool => ToBool(result, preview),
            TagType.Int => (int)Math.Round(ToDouble(result, preview)),
            TagType.Double => ToDouble(result, preview),
            TagType.String => result.ToString() ?? preview,
            _ => result,
        };
    }

    private static object? CastPreview(TagType tagType, string preview)
    {
        return tagType switch
        {
            TagType.Bool => ToBool(preview, "false"),
            TagType.Int => (int)Math.Round(ToDouble(preview, "0")),
            TagType.Double => ToDouble(preview, "0"),
            TagType.String => preview,
            _ => null,
        };
    }

    private static object? ConvertPreview(EmulatorTagEntity tag)
    {
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        return CastPreview(tagType, tag.Preview);
    }

    private static string ToPreview(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool ToBool(object value, string preview)
    {
        return value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            string stringValue => ToDouble(stringValue, preview) != 0,
            _ => ToDouble(value, preview) != 0,
        };
    }

    private static double ToDouble(object value, string preview)
    {
        return value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            decimal decimalValue => (double)decimalValue,
            bool boolValue => boolValue ? 1 : 0,
            string stringValue when double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
            IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
            _ => double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0,
        };
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private sealed record ScriptContent(string Path, string Content, string StateKey);

    private sealed class DbScriptSourceResolver(IReadOnlyDictionary<string, string> scripts) : SourceReferenceResolver
    {
        public override string? NormalizePath(string path, string? baseFilePath)
        {
            var normalized = TagScriptExecutionService.NormalizePath(path);
            if (scripts.ContainsKey(normalized))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(baseFilePath))
            {
                var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(baseDir))
                {
                    var relative = TagScriptExecutionService.NormalizePath($"{baseDir}/{path}");
                    if (scripts.ContainsKey(relative))
                    {
                        return relative;
                    }
                }
            }

            return normalized;
        }

        public override Stream OpenRead(string resolvedPath)
        {
            if (!scripts.TryGetValue(TagScriptExecutionService.NormalizePath(resolvedPath), out var content))
            {
                throw new FileNotFoundException($"Script '{resolvedPath}' was not found.");
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        public override string? ResolveReference(string path, string? baseFilePath)
        {
            var normalized = NormalizePath(path, baseFilePath);
            return normalized is not null && scripts.ContainsKey(normalized) ? normalized : null;
        }

        public override bool Equals(object? other) => ReferenceEquals(this, other);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    }
}

public sealed class TagScriptGlobals(
    TagScriptTagAccessor tag,
    DateTimeOffset now,
    TagScriptEmulatorContext emulator,
    TagScriptStateContext state)
{
    public TagScriptTagAccessor Tag { get; } = tag;
    public TagScriptTagAccessor Tags { get; } = tag;
    public DateTimeOffset Now { get; } = now;
    public TagScriptEmulatorContext Emulator { get; } = emulator;
    public TagScriptStateContext State { get; } = state;

    public double Random(double min, double max) => System.Random.Shared.NextDouble() * (max - min) + min;

    public void Log(string message) { }

    public void LogWarn(string message) { }

    public void LogError(string message) { }
}

public sealed class TagScriptTagAccessor(
    IDictionary<string, object?> values,
    Func<string, object?, object?> setStatic)
{
    public bool IsDirty { get; private set; }

    public TagScriptValue? this[string name]
    {
        get
        {
            return values.TryGetValue(name, out var value)
                ? new TagScriptValue(value)
                : null;
        }
    }

    public object? SetStatic(string name, object? value)
    {
        var typedValue = setStatic(name, value);
        values[name] = typedValue;
        IsDirty = true;
        return typedValue;
    }
}

public sealed class TagScriptValue(object? value)
{
    public object? Value { get; } = value;

    public double AsDouble(double fallback = 0)
    {
        return TelemetryValueGenerator.ToNumericValue(Value) ?? fallback;
    }

    public int AsInt(int fallback = 0) => (int)Math.Round(AsDouble(fallback));

    public bool AsBool(bool fallback = false)
    {
        return Value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            null => fallback,
            _ => AsDouble(fallback ? 1 : 0) != 0,
        };
    }

    public string AsString(string fallback = "") => Value?.ToString() ?? fallback;

    public override string ToString() => AsString();
}

public sealed record TagScriptEmulatorContext(string Id, string Name, string Status);

public sealed class TagScriptStateContext(
    bool isRunning,
    object? prevValue,
    double? prevNumericValue,
    DateTimeOffset? prevTimestamp,
    Dictionary<string, object?> values)
{
    public bool IsRunning { get; } = isRunning;
    public object? PrevValue { get; } = prevValue;
    public double? PrevNumericValue { get; } = prevNumericValue;
    public DateTimeOffset? PrevTimestamp { get; } = prevTimestamp;
    public bool IsDirty { get; private set; }

    public TagScriptValue? this[string key] => Get(key);

    public TagScriptValue? Get(string key)
    {
        return values.TryGetValue(key, out var value)
            ? new TagScriptValue(UnwrapJsonValue(value))
            : null;
    }

    public T? Get<T>(string key, T? fallback = default)
    {
        return values.TryGetValue(key, out var value)
            ? ConvertStateValue(value, fallback)
            : fallback;
    }

    public void Set(string key, object? value)
    {
        values[key] = value;
        IsDirty = true;
    }

    public bool Remove(string key)
    {
        var removed = values.Remove(key);
        IsDirty |= removed;
        return removed;
    }

    public void Clear()
    {
        if (values.Count == 0)
        {
            return;
        }

        values.Clear();
        IsDirty = true;
    }

    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        return new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static T? ConvertStateValue<T>(object? value, T? fallback)
    {
        value = UnwrapJsonValue(value);
        if (value is null)
        {
            return fallback;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            if (value is IConvertible)
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            return fallback;
        }

        return fallback;
    }

    private static object? UnwrapJsonValue(object? value)
    {
        if (value is not System.Text.Json.JsonElement json)
        {
            return value;
        }

        return json.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Number when json.TryGetInt64(out var longValue) => longValue,
            System.Text.Json.JsonValueKind.Number when json.TryGetDouble(out var doubleValue) => doubleValue,
            System.Text.Json.JsonValueKind.String => json.GetString(),
            System.Text.Json.JsonValueKind.Null => null,
            _ => json.GetRawText(),
        };
    }
}
