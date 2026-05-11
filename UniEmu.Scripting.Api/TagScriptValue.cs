namespace UniEmu.Scripting.Api;

/// <summary>
/// Значение тега вместе с его метаданными, доступное из скрипта.
/// </summary>
[ScriptingApi]
public sealed class TagScriptValue
{
    /// <summary>
    /// Уникальный ключ тега, используемый для обращения к нему в скриптах.
    /// </summary>
    [ScriptingApi]
    public string Key { get; }

    /// <summary>
    /// Отображаемое имя тега.
    /// </summary>
    [ScriptingApi]
    public string Name { get; }

    /// <summary>
    /// Текущее значение тега.
    /// </summary>
    [ScriptingApi]
    public object? Value { get; set; }

    /// <summary>
    /// Тип текущего значения тега.
    /// </summary>
    [ScriptingApi]
    public TagScriptValueType Type { get; }

    /// <summary>
    /// Время получения или изменения значения, если оно известно.
    /// </summary>
    [ScriptingApi]
    public DateTimeOffset? Timestamp { get; }

    /// <summary>
    /// Создает описание значения тега для передачи в скрипт.
    /// </summary>
    /// <param name="key">Уникальный ключ тега.</param>
    /// <param name="name">Отображаемое имя тега.</param>
    /// <param name="value">Текущее значение тега.</param>
    /// <param name="type">Тип значения тега.</param>
    /// <param name="timestamp">Время получения или изменения значения, если оно известно.</param>
    public TagScriptValue(string key, string name, object? value, TagScriptValueType type, DateTimeOffset? timestamp)
    {
        Key = key;
        Name = name;
        Value = value;
        Type = type;
        Timestamp = timestamp;
    }
}
