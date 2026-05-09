using System.Text.Json;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;

namespace UniEmu.Tests.Common;

public sealed class UniEmuJsonTests
{
    [Fact]
    public void Apply_SerializesEmulatorStatusAsString_ForRealtimePayloads()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        UniEmuJson.Apply(options);
        var emulator = new EmulatorDto(
            "em-1",
            "CNC",
            EmulatorStatus.Running,
            18,
            "https://example.test",
            5,
            LastRun: null,
            NextRun: null,
            LastError: null,
            TagsCount: 1,
            UptimeSec: 10,
            TotalRequests: 2);

        var json = JsonSerializer.Serialize(emulator, options);

        Assert.Contains("\"status\":\"Running\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\":0", json, StringComparison.Ordinal);
    }
}
