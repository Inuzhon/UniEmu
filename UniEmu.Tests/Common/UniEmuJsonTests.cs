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

    [Fact]
    public void SpecialParameter_MatchesUniversalProtocolContract()
    {
        var expected = new (SpecialParameter Parameter, int Value)[]
        {
            (SpecialParameter.None, 0),
            (SpecialParameter.PrgName, 1),
            (SpecialParameter.PartCounter, 2),
            (SpecialParameter.ErrorNum, 3),
            (SpecialParameter.FeedOvr, 4),
            (SpecialParameter.SpindleOvr, 5),
            (SpecialParameter.JogOvr, 6),
            (SpecialParameter.FrameNum, 7),
            (SpecialParameter.FrameText, 8),
            (SpecialParameter.ToolNum, 9),
            (SpecialParameter.WorkMode, 10),
            (SpecialParameter.SystemState, 11),
            (SpecialParameter.MachineReadiness, 12),
            (SpecialParameter.TechnologicalStop, 13),
            (SpecialParameter.EmergencyStop, 14),
            (SpecialParameter.FeedRate, 15),
            (SpecialParameter.ErrorText, 16),
            (SpecialParameter.CycleTime, 17),
            (SpecialParameter.SpindleSpeed, 18),
            (SpecialParameter.SpindleLoad, 19),
            (SpecialParameter.AxisLoad, 20),
            (SpecialParameter.AxisPosition, 21),
            (SpecialParameter.Message, 22),
            (SpecialParameter.CNCModel, 23),
            (SpecialParameter.FirmwareVersion, 24),
            (SpecialParameter.SerialNumber, 25),
            (SpecialParameter.PLCVersion, 26),
            (SpecialParameter.Subprogram, 27),
        };

        var actual = Enum.GetValues<SpecialParameter>()
            .Select(parameter => (Parameter: parameter, Value: (int)parameter))
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
