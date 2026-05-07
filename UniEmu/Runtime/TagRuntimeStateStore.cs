using System.Collections.Concurrent;

namespace UniEmu.Runtime;

public sealed record TagRuntimeValue(string TagId, string TagName, double Value, DateTimeOffset Timestamp);

public sealed class TagRuntimeStateStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TagRuntimeValue>> values = new(StringComparer.Ordinal);

    public void Set(string emulatorId, string tagId, string tagName, double value, DateTimeOffset timestamp)
    {
        var emulatorValues = values.GetOrAdd(emulatorId, _ => new ConcurrentDictionary<string, TagRuntimeValue>(StringComparer.Ordinal));
        emulatorValues[tagId] = new TagRuntimeValue(tagId, tagName, value, timestamp);
    }

    public bool TryGet(string emulatorId, string tagId, out TagRuntimeValue value)
    {
        if (values.TryGetValue(emulatorId, out var emulatorValues)
            && emulatorValues.TryGetValue(tagId, out var runtimeValue))
        {
            value = runtimeValue;
            return true;
        }

        value = default!;
        return false;
    }

    public void Remove(string emulatorId, string tagId)
    {
        if (values.TryGetValue(emulatorId, out var emulatorValues))
        {
            emulatorValues.TryRemove(tagId, out _);
        }
    }

    public void ClearEmulator(string emulatorId)
    {
        values.TryRemove(emulatorId, out _);
    }
}
