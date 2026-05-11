using UniEmu.Contracts.Enums;

namespace UniEmu.Runtime.Scripting.UserScripts;

public sealed class TagScriptValue
{
    public string Key { get; }
    public string Name { get; }
    public object? Value { get; set; }
    public TagType Type { get; }

    public DateTimeOffset? Timestamp { get; }

    public TagScriptValue(string key, string name, object? value, TagType type, DateTimeOffset? timestamp)
    {
        Key = key;
        Name = name;
        Value = value;
        Type = type;
        Timestamp = timestamp;
    }
}
