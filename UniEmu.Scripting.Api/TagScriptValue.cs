namespace UniEmu.Scripting.Api;

public sealed class TagScriptValue
{
    public string Key { get; }

    public string Name { get; }

    public object? Value { get; set; }

    public TagScriptValueType Type { get; }

    public DateTimeOffset? Timestamp { get; }

    public TagScriptValue(string key, string name, object? value, TagScriptValueType type, DateTimeOffset? timestamp)
    {
        Key = key;
        Name = name;
        Value = value;
        Type = type;
        Timestamp = timestamp;
    }
}
