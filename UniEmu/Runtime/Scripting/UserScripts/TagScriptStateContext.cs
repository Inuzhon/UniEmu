using System.Globalization;

namespace UniEmu.Runtime.Scripting.UserScripts;

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
            return;

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

    public IReadOnlyDictionary<string, TagScriptValue> Snapshot()
    {
        return new Dictionary<string, TagScriptValue>(values, StringComparer.OrdinalIgnoreCase);
    }

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
        if (value is not System.Text.Json.JsonElement json)
            return value;

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
