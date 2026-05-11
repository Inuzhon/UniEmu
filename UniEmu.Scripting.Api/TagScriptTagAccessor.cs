namespace UniEmu.Scripting.Api;

/// <summary>
/// Предоставляет скрипту доступ к значениям тегов и позволяет изменять статические теги.
/// </summary>
public sealed class TagScriptTagAccessor
{
    private readonly IDictionary<string, TagScriptValue> _values;
    private readonly Func<string, object?, object?> _setStatic;

    /// <summary>
    /// Показывает, изменял ли скрипт значения тегов через этот объект.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Создает объект доступа к тегам для выполнения скрипта.
    /// </summary>
    /// <param name="values">Набор значений тегов, доступных по ключу.</param>
    /// <param name="setStatic">Функция записи значения в статический тег с приведением к ожидаемому типу.</param>
    public TagScriptTagAccessor(IDictionary<string, TagScriptValue> values, Func<string, object?, object?> setStatic)
    {
        _values = values;
        _setStatic = setStatic;
    }

    /// <summary>
    /// Пытается получить значение тега по его ключу.
    /// </summary>
    /// <param name="keyName">Ключ тега.</param>
    /// <param name="tagValue">Найденное значение тега или <see langword="null"/>, если тег не найден.</param>
    /// <returns><see langword="true"/>, если тег найден; иначе <see langword="false"/>.</returns>
    public bool TryGetValue(string keyName, out TagScriptValue? tagValue)
    {
        return _values.TryGetValue(keyName, out tagValue);
    }

    /// <summary>
    /// Пытается изменить значение статического тега по его ключу.
    /// </summary>
    /// <param name="keyName">Ключ изменяемого тега.</param>
    /// <param name="value">Новое значение тега.</param>
    /// <returns><see langword="true"/>, если тег найден и обновлен; иначе <see langword="false"/>.</returns>
    public bool TrySetValue(string keyName, object? value)
    {
        if (!TryGetValue(keyName, out var tagScriptValue) || tagScriptValue is null)
            return false;

        var typedValue = _setStatic(keyName, value);

        tagScriptValue.Value = typedValue;
        _values[keyName] = tagScriptValue;

        IsDirty = true;

        return true;
    }
}
