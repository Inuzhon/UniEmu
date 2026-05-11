namespace UniEmu.Runtime.Models;

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
