using System.Collections.Concurrent;

namespace UniEmu.Runtime;

/// <summary>
/// Последнее runtime-значение тега внутри одного эмулятора.
/// </summary>
/// <param name="TagId">Идентификатор тега.</param>
/// <param name="TagName">Отображаемое имя тега.</param>
/// <param name="Value">Типизированное значение тега.</param>
/// <param name="NumericValue">Числовое представление значения, если оно доступно.</param>
/// <param name="Timestamp">Время расчета значения.</param>
public sealed record TagRuntimeValue(
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);

/// <summary>
/// Значение тега в снимке runtime-состояния по всем эмуляторам.
/// </summary>
/// <param name="EmulatorId">Идентификатор эмулятора.</param>
/// <param name="TagId">Идентификатор тега.</param>
/// <param name="TagName">Отображаемое имя тега.</param>
/// <param name="Value">Типизированное значение тега.</param>
/// <param name="NumericValue">Числовое представление значения, если оно доступно.</param>
/// <param name="Timestamp">Время расчета значения.</param>
public sealed record TagRuntimeSnapshotValue(
    string EmulatorId,
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);

/// <summary>
/// Потокобезопасное in-memory хранилище последних значений тегов runtime.
/// </summary>
public sealed class TagRuntimeStateStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TagRuntimeValue>> _values = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<TagRuntimeValue>>> _waiters = new(StringComparer.Ordinal);

    /// <summary>
    /// Сохраняет новое значение тега и пробуждает ожидающие операции чтения.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="tagName">Отображаемое имя тега.</param>
    /// <param name="value">Типизированное значение тега.</param>
    /// <param name="numericValue">Числовое представление значения, если оно доступно.</param>
    /// <param name="timestamp">Время расчета значения.</param>
    public void Set(string emulatorId, string tagId, string tagName, object? value, double? numericValue, DateTimeOffset timestamp)
    {
        var emulatorValues = _values.GetOrAdd(emulatorId, _ => new ConcurrentDictionary<string, TagRuntimeValue>(StringComparer.Ordinal));
        var runtimeValue = new TagRuntimeValue(tagId, tagName, value, numericValue, timestamp);
        emulatorValues[tagId] = runtimeValue;

        if (_waiters.TryGetValue(emulatorId, out var emulatorWaiters)
            && emulatorWaiters.TryRemove(tagId, out var waiter))
        {
            waiter.TrySetResult(runtimeValue);
        }
    }

    /// <summary>
    /// Пытается получить последнее известное значение тега.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="value">Найденное значение тега.</param>
    /// <returns><see langword="true"/>, если значение найдено.</returns>
    public bool TryGet(string emulatorId, string tagId, out TagRuntimeValue value)
    {
        if (_values.TryGetValue(emulatorId, out var emulatorValues)
            && emulatorValues.TryGetValue(tagId, out var runtimeValue))
        {
            value = runtimeValue;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Возвращает снимок всех известных runtime-значений.
    /// </summary>
    /// <returns>Список значений по всем эмуляторам.</returns>
    public IReadOnlyList<TagRuntimeSnapshotValue> Snapshot()
    {
        return _values
            .SelectMany(emulator => emulator.Value.Values.Select(value => new TagRuntimeSnapshotValue(
                emulator.Key,
                value.TagId,
                value.TagName,
                value.Value,
                value.NumericValue,
                value.Timestamp)))
            .ToList();
    }

    /// <summary>
    /// Ожидает значение тега не старше указанного времени.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="notOlderThan">Минимально допустимое время расчета значения.</param>
    /// <param name="timeout">Максимальное время ожидания.</param>
    /// <param name="cancellationToken">Токен отмены ожидания.</param>
    /// <returns>Актуальное значение или <see langword="null"/> при тайм-ауте.</returns>
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

        var emulatorWaiters = _waiters.GetOrAdd(emulatorId, _ => new ConcurrentDictionary<string, TaskCompletionSource<TagRuntimeValue>>(StringComparer.Ordinal));
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

    /// <summary>
    /// Удаляет runtime-значение тега и отменяет ожидания по нему.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    public void Remove(string emulatorId, string tagId)
    {
        if (_values.TryGetValue(emulatorId, out var emulatorValues))
        {
            emulatorValues.TryRemove(tagId, out _);
        }

        if (_waiters.TryGetValue(emulatorId, out var emulatorWaiters)
            && emulatorWaiters.TryRemove(tagId, out var waiter))
        {
            waiter.TrySetCanceled();
        }
    }

    /// <summary>
    /// Очищает все runtime-значения эмулятора и отменяет связанные ожидания.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    public void ClearEmulator(string emulatorId)
    {
        _values.TryRemove(emulatorId, out _);

        if (_waiters.TryRemove(emulatorId, out var emulatorWaiters))
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
