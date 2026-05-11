namespace UniEmu.Scripting.Api;

public sealed class TagScriptTagAccessor
{
    private readonly IDictionary<string, TagScriptValue> _values;
    private readonly Func<string, object?, object?> _setStatic;

    public bool IsDirty { get; private set; }

    public TagScriptTagAccessor(IDictionary<string, TagScriptValue> values, Func<string, object?, object?> setStatic)
    {
        _values = values;
        _setStatic = setStatic;
    }

    /// <summary>
    /// Attempts to get the tag value associated with the specified key.
    /// </summary>
    /// <param name="keyName">The key whose associated value is requested.</param>
    /// <param name="tagValue">When this method returns, contains the value associated with the specified key if found; otherwise, null.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    public bool TryGetValue(string keyName, out TagScriptValue? tagValue)
    {
        return _values.TryGetValue(keyName, out tagValue);
    }

    /// <summary>
    /// Attempts to set a static tag value by key.
    /// </summary>
    /// <param name="keyName">The key of the tag to update.</param>
    /// <param name="value">The value to assign to the tag.</param>
    /// <returns>true if the tag existed and was updated; otherwise, false.</returns>
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
