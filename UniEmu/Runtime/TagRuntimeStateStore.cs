using System.Collections.Concurrent;

namespace UniEmu.Runtime;

public sealed record TagRuntimeValue(
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);

public sealed record TagRuntimeSnapshotValue(
    string EmulatorId,
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);

public sealed class TagRuntimeStateStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TagRuntimeValue>> values = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<TagRuntimeValue>>> waiters = new(StringComparer.Ordinal);

    public void Set(string emulatorId, string tagId, string tagName, object? value, double? numericValue, DateTimeOffset timestamp)
    {
        var emulatorValues = values.GetOrAdd(emulatorId, _ => new ConcurrentDictionary<string, TagRuntimeValue>(StringComparer.Ordinal));
        var runtimeValue = new TagRuntimeValue(tagId, tagName, value, numericValue, timestamp);
        emulatorValues[tagId] = runtimeValue;

        if (waiters.TryGetValue(emulatorId, out var emulatorWaiters)
            && emulatorWaiters.TryRemove(tagId, out var waiter))
        {
            waiter.TrySetResult(runtimeValue);
        }
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

    public IReadOnlyList<TagRuntimeSnapshotValue> Snapshot()
    {
        return values
            .SelectMany(emulator => emulator.Value.Values.Select(value => new TagRuntimeSnapshotValue(
                emulator.Key,
                value.TagId,
                value.TagName,
                value.Value,
                value.NumericValue,
                value.Timestamp)))
            .ToList();
    }

    public async Task<TagRuntimeValue?> WaitForValueAsync(
        string emulatorId,
        string tagId,
        DateTimeOffset notOlderThan,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (TryGet(emulatorId, tagId, out var existingValue) && existingValue.Timestamp >= notOlderThan)
        {
            return existingValue;
        }

        var emulatorWaiters = waiters.GetOrAdd(emulatorId, _ => new ConcurrentDictionary<string, TaskCompletionSource<TagRuntimeValue>>(StringComparer.Ordinal));
        var waiter = new TaskCompletionSource<TagRuntimeValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        emulatorWaiters[tagId] = waiter;

        if (TryGet(emulatorId, tagId, out existingValue) && existingValue.Timestamp >= notOlderThan)
        {
            TryRemoveWaiter(emulatorWaiters, tagId, waiter);
            return existingValue;
        }

        try
        {
            return await waiter.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            TryRemoveWaiter(emulatorWaiters, tagId, waiter);
        }
    }

    public void Remove(string emulatorId, string tagId)
    {
        if (values.TryGetValue(emulatorId, out var emulatorValues))
        {
            emulatorValues.TryRemove(tagId, out _);
        }

        if (waiters.TryGetValue(emulatorId, out var emulatorWaiters)
            && emulatorWaiters.TryRemove(tagId, out var waiter))
        {
            waiter.TrySetCanceled();
        }
    }

    public void ClearEmulator(string emulatorId)
    {
        values.TryRemove(emulatorId, out _);

        if (waiters.TryRemove(emulatorId, out var emulatorWaiters))
        {
            foreach (var waiter in emulatorWaiters.Values)
            {
                waiter.TrySetCanceled();
            }
        }
    }

    private static void TryRemoveWaiter(
        ConcurrentDictionary<string, TaskCompletionSource<TagRuntimeValue>> emulatorWaiters,
        string tagId,
        TaskCompletionSource<TagRuntimeValue> waiter)
    {
        ((ICollection<KeyValuePair<string, TaskCompletionSource<TagRuntimeValue>>>)emulatorWaiters)
            .Remove(new KeyValuePair<string, TaskCompletionSource<TagRuntimeValue>>(tagId, waiter));
    }
}
