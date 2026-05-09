using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TagRuntimeStateStoreTests
{
    [Fact]
    public void TryGet_ReturnsLatestStoredValue_ForEmulatorAndTag()
    {
        var store = new TagRuntimeStateStore();
        var timestamp = DateTimeOffset.Parse("2026-05-09T10:00:00Z");

        store.Set("emu-1", "tag-1", "Temperature", 42.5, 42.5, timestamp);

        var found = store.TryGet("emu-1", "tag-1", out var value);

        Assert.True(found);
        Assert.Equal("tag-1", value.TagId);
        Assert.Equal("Temperature", value.TagName);
        Assert.Equal(42.5, value.Value);
        Assert.Equal(42.5, value.NumericValue);
        Assert.Equal(timestamp, value.Timestamp);
    }

    [Fact]
    public async Task WaitForValueAsync_ReturnsExistingValue_WhenItIsFreshEnough()
    {
        var store = new TagRuntimeStateStore();
        var timestamp = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        store.Set("emu-1", "tag-1", "Temperature", 42.5, 42.5, timestamp);

        var value = await store.WaitForValueAsync(
            "emu-1",
            "tag-1",
            timestamp.AddSeconds(-1),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.NotNull(value);
        Assert.Equal(42.5, value.Value);
    }

    [Fact]
    public async Task WaitForValueAsync_WaitsForFutureValue_WhenExistingValueIsTooOld()
    {
        var store = new TagRuntimeStateStore();
        var oldTimestamp = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var freshTimestamp = oldTimestamp.AddSeconds(10);
        store.Set("emu-1", "tag-1", "Temperature", 40.0, 40.0, oldTimestamp);

        var waitTask = store.WaitForValueAsync(
            "emu-1",
            "tag-1",
            freshTimestamp,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        store.Set("emu-1", "tag-1", "Temperature", 41.0, 41.0, freshTimestamp);

        var value = await waitTask;

        Assert.NotNull(value);
        Assert.Equal(41.0, value.Value);
        Assert.Equal(freshTimestamp, value.Timestamp);
    }

    [Fact]
    public async Task WaitForValueAsync_ReturnsNull_WhenNoValueArrivesBeforeTimeout()
    {
        var store = new TagRuntimeStateStore();

        var value = await store.WaitForValueAsync(
            "emu-1",
            "tag-1",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.Null(value);
    }
}
