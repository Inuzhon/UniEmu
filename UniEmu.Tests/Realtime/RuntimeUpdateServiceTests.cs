using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Realtime;

namespace UniEmu.Tests.Realtime;

public sealed class RuntimeUpdateServiceTests
{
    [Fact]
    public async Task PublishTelemetryAsync_SendsTelemetryToAllAndEmulatorGroups()
    {
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = new RuntimeUpdateService(broadcaster);
        var telemetry = new TelemetryPointDto(
            DateTimeOffset.Parse("2026-05-09T10:00:00Z"),
            new Dictionary<string, double> { ["Temperature"] = 42.5 });

        await service.PublishTelemetryAsync("em-1", telemetry, CancellationToken.None);

        var update = Assert.Single(broadcaster.TelemetryUpdates);
        Assert.Equal("em-1", update.Update.EmulatorId);
        Assert.Equal(telemetry.Timestamp, update.Update.Point.Timestamp);
        Assert.Equal(42.5, update.Update.Point.Values["Temperature"]);
        Assert.Equal(new[] { "runtime:all", "emulator:em-1" }, update.Groups);
    }

    [Fact]
    public async Task PublishTagValueAsync_SendsTagValueToAllAndEmulatorGroups()
    {
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = new RuntimeUpdateService(broadcaster);

        await service.PublishTagValueAsync(
            new RuntimeTagValueUpdateDto(
                "em-1",
                "tag-1",
                "Temperature",
                42.5,
                42.5,
                DateTimeOffset.Parse("2026-05-09T10:00:00Z")),
            CancellationToken.None);

        var update = Assert.Single(broadcaster.TagValueUpdates);
        Assert.Equal("em-1", update.Update.EmulatorId);
        Assert.Equal("tag-1", update.Update.TagId);
        Assert.Equal("Temperature", update.Update.TagName);
        Assert.Equal(42.5, update.Update.Value);
        Assert.Equal(new[] { "runtime:all", "emulator:em-1" }, update.Groups);
    }

    [Fact]
    public async Task PublishEmulatorUpdatedAsync_SendsEmulatorToAllAndEmulatorGroups()
    {
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = new RuntimeUpdateService(broadcaster);
        var emulator = new EmulatorDto(
            "em-1",
            "CNC",
            EmulatorStatus.Running,
            18,
            "https://example.test",
            5,
            DateTimeOffset.Parse("2026-05-09T10:00:00Z"),
            DateTimeOffset.Parse("2026-05-09T10:00:05Z"),
            null,
            3,
            10,
            2);

        await service.PublishEmulatorUpdatedAsync(emulator, CancellationToken.None);

        var update = Assert.Single(broadcaster.EmulatorUpdates);
        Assert.Equal(emulator, update.Emulator);
        Assert.Equal(new[] { "runtime:all", "emulator:em-1" }, update.Groups);
    }

    [Fact]
    public async Task PublishEventCreatedAsync_SendsEventToAllAndEmulatorGroups()
    {
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = new RuntimeUpdateService(broadcaster);
        var ev = new SystemEventDto(
            "ev-1",
            "em-1",
            "CNC",
            EventLevel.Success,
            "Packet sent",
            DateTimeOffset.Parse("2026-05-09T10:00:00Z"));

        await service.PublishEventCreatedAsync(ev, CancellationToken.None);

        var update = Assert.Single(broadcaster.EventUpdates);
        Assert.Equal(ev, update.Event);
        Assert.Equal(new[] { "runtime:all", "emulator:em-1" }, update.Groups);
    }

    private sealed class RecordingRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public List<(RuntimeTelemetryUpdateDto Update, IReadOnlyList<string> Groups)> TelemetryUpdates { get; } = [];
        public List<(RuntimeTagValueUpdateDto Update, IReadOnlyList<string> Groups)> TagValueUpdates { get; } = [];
        public List<(EmulatorDto Emulator, IReadOnlyList<string> Groups)> EmulatorUpdates { get; } = [];
        public List<(SystemEventDto Event, IReadOnlyList<string> Groups)> EventUpdates { get; } = [];

        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            TelemetryUpdates.Add((update, groups));
            return Task.CompletedTask;
        }

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            TagValueUpdates.Add((update, groups));
            return Task.CompletedTask;
        }

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            EmulatorUpdates.Add((emulator, groups));
            return Task.CompletedTask;
        }

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            EventUpdates.Add((ev, groups));
            return Task.CompletedTask;
        }
    }
}
