using System.Text.Json;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Runtime;

public sealed class TagScriptStateContextTests
{
    [Fact]
    public void Get_ReturnsFallback_WhenKeyIsMissingOrCannotConvert()
    {
        var state = CreateState(new Dictionary<string, object?>
        {
            ["text"] = "abc",
        });

        Assert.Equal(10, state.Get("missing", 10));
        Assert.Equal(20, state.Get("text", 20));
    }

    [Fact]
    public void Get_ConvertsPersistedJsonValuesToRequestedTypes()
    {
        using var document = JsonDocument.Parse("""{"count":3,"ratio":1.5,"enabled":true,"name":"pump"}""");
        var state = CreateState(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));

        Assert.Equal(3, state.Get("count", 0));
        Assert.Equal(1.5d, state.Get("ratio", 0d));
        Assert.True(state.Get("enabled", false));
        Assert.Equal("pump", state.Get("name", string.Empty));
    }

    [Fact]
    public void Set_AddsOrUpdatesValueAndMarksStateDirty()
    {
        var state = CreateState(new Dictionary<string, object?>
        {
            ["count"] = 1,
        });

        state.Set("count", 2);
        state.Set("name", "pump");

        Assert.True(state.IsDirty);
        Assert.Equal(2, state.Get("count", 0));
        Assert.Equal("pump", state.Get("name", string.Empty));
        Assert.Equal(TagScriptValueType.String, state["name"]?.Type);
    }

    [Fact]
    public void RemoveAndClear_MarkStateDirtyOnlyWhenValuesChange()
    {
        var empty = CreateState(new Dictionary<string, object?>());

        Assert.False(empty.Remove("missing"));
        empty.Clear();
        Assert.False(empty.IsDirty);

        var state = CreateState(new Dictionary<string, object?>
        {
            ["count"] = 1,
            ["enabled"] = true,
        });

        Assert.True(state.Remove("count"));
        Assert.True(state.IsDirty);
        Assert.Null(state.Get("count"));

        state.Clear();

        Assert.Empty(state.Snapshot());
    }

    [Fact]
    public void Snapshot_ReturnsPlainValuesWithCaseInsensitiveKeys()
    {
        var state = CreateState(new Dictionary<string, object?>
        {
            ["Count"] = 1,
        });

        state.Set("enabled", true);
        var snapshot = state.Snapshot();

        Assert.Equal(1, snapshot["count"]);
        Assert.Equal(true, snapshot["ENABLED"]);
    }

    private static TagScriptStateContext CreateState(Dictionary<string, object?> values)
    {
        return new TagScriptStateContext(
            isRunning: true,
            prevValue: null,
            prevNumericValue: null,
            prevTimestamp: null,
            values.ToDictionary(
                value => value.Key,
                value => new TagScriptValue(value.Key, value.Key, value.Value, TagScriptValueType.String, null),
                StringComparer.OrdinalIgnoreCase));
    }
}
