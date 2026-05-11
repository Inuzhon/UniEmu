using System.Globalization;
using System.Text.Json;

namespace UniEmu.Scripting.Api;

public sealed class TagScriptStateContext(
    bool isRunning,
    object? prevValue,
    double? prevNumericValue,
    DateTimeOffset? prevTimestamp,
    Dictionary<string, TagScriptValue> values)
{
    public bool IsRunning { get; } = isRunning;

    public object? PrevValue { get; } = prevValue;

    public double? PrevNumericValue { get; } = prevNumericValue;

    public DateTimeOffset? PrevTimestamp { get; } = prevTimestamp;

    public bool IsDirty { get; private set; }

    public TagScriptValue? this[string key] => Get(key);

    public TagScriptValue? Get(string key)
    {
        return values.GetValueOrDefault(key);
    }

    public T? Get<T>(string key, T? fallback = default)
    {
        return values.TryGetValue(key, out var tagScriptValue)
            ? ConvertStateValue(tagScriptValue.Value, fallback)
            : fallback;
    }

    public void Set(string key, object? value)
    {
        if (!values.TryGetValue(key, out var tagScriptValue))
        {
            values[key] = new TagScriptValue(key, key, value, InferType(value), null);
            IsDirty = true;
            return;
        }

        tagScriptValue.Value = value;
        values[key] = tagScriptValue;

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
            return;

        values.Clear();
        IsDirty = true;
    }

    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        return values.ToDictionary(
            value => value.Key,
            value => value.Value.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static TagScriptValueType InferType(object? value) => UnwrapJsonValue(value) switch
    {
        bool => TagScriptValueType.Bool,
        byte or short or int or long => TagScriptValueType.Int,
        float or double or decimal => TagScriptValueType.Double,
        _ => TagScriptValueType.String,
    };

    private static T? ConvertStateValue<T>(object? value, T? fallback)
    {
        value = UnwrapJsonValue(value);
        if (value is null)
            return fallback;

        if (value is T typedValue)
            return typedValue;

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
        if (value is not JsonElement json)
            return value;

        return json.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when json.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when json.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Null => null,
            _ => json.GetRawText(),
        };
    }
}
