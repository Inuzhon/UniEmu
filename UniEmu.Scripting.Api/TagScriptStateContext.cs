using System.Globalization;
using System.Text.Json;

namespace UniEmu.Scripting.Api;

/// <summary>
/// Хранит состояние скрипта между вычислениями одного тега.
/// </summary>
/// <param name="isRunning">Признак того, что вычисление тега уже выполнялось ранее.</param>
/// <param name="prevValue">Предыдущее рассчитанное значение тега.</param>
/// <param name="prevNumericValue">Предыдущее рассчитанное значение тега, приведенное к числу, если это возможно.</param>
/// <param name="prevTimestamp">Время предыдущего рассчитанного значения тега, если оно известно.</param>
/// <param name="values">Сохраненные пользовательские значения состояния.</param>
public sealed class TagScriptStateContext(
    bool isRunning,
    object? prevValue,
    double? prevNumericValue,
    DateTimeOffset? prevTimestamp,
    Dictionary<string, TagScriptValue> values)
{
    /// <summary>
    /// Показывает, что для тега уже было хотя бы одно вычисление.
    /// </summary>
    public bool IsRunning { get; } = isRunning;

    /// <summary>
    /// Предыдущее рассчитанное значение тега.
    /// </summary>
    public object? PrevValue { get; } = prevValue;

    /// <summary>
    /// Предыдущее рассчитанное значение тега, приведенное к числу, если это возможно.
    /// </summary>
    public double? PrevNumericValue { get; } = prevNumericValue;

    /// <summary>
    /// Время предыдущего рассчитанного значения тега, если оно известно.
    /// </summary>
    public DateTimeOffset? PrevTimestamp { get; } = prevTimestamp;

    /// <summary>
    /// Показывает, изменялось ли состояние во время текущего выполнения скрипта.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Получает сохраненное значение состояния по ключу.
    /// </summary>
    /// <param name="key">Ключ значения состояния.</param>
    /// <returns>Значение состояния или <see langword="null"/>, если ключ не найден.</returns>
    public TagScriptValue? this[string key] => Get(key);

    /// <summary>
    /// Получает сохраненное значение состояния по ключу.
    /// </summary>
    /// <param name="key">Ключ значения состояния.</param>
    /// <returns>Значение состояния или <see langword="null"/>, если ключ не найден.</returns>
    public TagScriptValue? Get(string key)
    {
        return values.GetValueOrDefault(key);
    }

    /// <summary>
    /// Получает сохраненное значение состояния и приводит его к указанному типу.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип значения.</typeparam>
    /// <param name="key">Ключ значения состояния.</param>
    /// <param name="fallback">Значение, возвращаемое при отсутствии ключа или ошибке приведения типа.</param>
    /// <returns>Сохраненное значение, приведенное к типу <typeparamref name="T"/>, либо <paramref name="fallback"/>.</returns>
    public T? Get<T>(string key, T? fallback = default)
    {
        return values.TryGetValue(key, out var tagScriptValue)
            ? ConvertStateValue(tagScriptValue.Value, fallback)
            : fallback;
    }

    /// <summary>
    /// Сохраняет значение состояния по ключу.
    /// </summary>
    /// <param name="key">Ключ значения состояния.</param>
    /// <param name="value">Новое значение состояния.</param>
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

    /// <summary>
    /// Удаляет сохраненное значение состояния по ключу.
    /// </summary>
    /// <param name="key">Ключ удаляемого значения состояния.</param>
    /// <returns><see langword="true"/>, если значение было найдено и удалено; иначе <see langword="false"/>.</returns>
    public bool Remove(string key)
    {
        var removed = values.Remove(key);
        IsDirty |= removed;
        return removed;
    }

    /// <summary>
    /// Очищает все сохраненные значения состояния.
    /// </summary>
    public void Clear()
    {
        if (values.Count == 0)
            return;

        values.Clear();
        IsDirty = true;
    }

    /// <summary>
    /// Создает снимок сохраненного состояния в виде словаря простых значений.
    /// </summary>
    /// <returns>Словарь значений состояния, где ключом является имя значения.</returns>
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
