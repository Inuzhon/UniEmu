using Microsoft.AspNetCore.SignalR;
using Moq;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Realtime;

namespace UniEmu.Tests.Realtime;

public sealed class RuntimeUpdatesHubSignalRTests
{
    [Fact]
    public async Task Hub_AddsConnectionToAllGroup_OnConnectAndSubscribeAll()
    {
        var groups = new Mock<IGroupManager>();
        var hub = CreateHub("connection-1", groups.Object);

        await hub.OnConnectedAsync();
        await hub.SubscribeAll();

        groups.Verify(
            groupManager => groupManager.AddToGroupAsync("connection-1", RuntimeUpdateService.AllGroup, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Hub_ManagesEmulatorGroupSubscriptions()
    {
        var groups = new Mock<IGroupManager>();
        var hub = CreateHub("connection-1", groups.Object);

        await hub.SubscribeEmulator("em-1");
        await hub.UnsubscribeEmulator("em-1");

        groups.Verify(
            groupManager => groupManager.AddToGroupAsync("connection-1", RuntimeUpdateService.EmulatorGroup("em-1"), It.IsAny<CancellationToken>()),
            Times.Once);
        groups.Verify(
            groupManager => groupManager.RemoveFromGroupAsync("connection-1", RuntimeUpdateService.EmulatorGroup("em-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignalRBroadcaster_ForwardsRuntimeUpdatesToRequestedGroups()
    {
        var client = new Mock<IRuntimeUpdatesClient>();
        var groups = new[] { RuntimeUpdateService.AllGroup, RuntimeUpdateService.EmulatorGroup("em-1") };
        var clients = new Mock<IHubClients<IRuntimeUpdatesClient>>();
        clients.Setup(hubClients => hubClients.Groups(groups)).Returns(client.Object);
        var hubContext = new Mock<IHubContext<RuntimeUpdatesHub, IRuntimeUpdatesClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);
        var broadcaster = new SignalRRuntimeUpdateBroadcaster(hubContext.Object);
        var telemetry = new RuntimeTelemetryUpdateDto(
            "em-1",
            new TelemetryPointDto(DateTimeOffset.Parse("2026-05-10T12:00:00Z"), new Dictionary<string, object?>()));
        var tagValue = new RuntimeTagValueUpdateDto("em-1", "tag-1", "Tag", 1d, 1d, DateTimeOffset.Parse("2026-05-10T12:00:01Z"));
        var emulator = new EmulatorDto(
            "em-1",
            "Main emulator",
            EmulatorStatus.Running,
            18,
            "http://localhost",
            1,
            null,
            null,
            null,
            TagsCount: 0,
            UptimeSec: 0,
            TotalRequests: 0);
        var ev = new SystemEventDto(
            "ev-1",
            "em-1",
            "Main emulator",
            EventLevel.Info,
            "Started",
            DateTimeOffset.Parse("2026-05-10T12:00:02Z"));

        await broadcaster.SendTelemetryAsync(telemetry, groups, CancellationToken.None);
        await broadcaster.SendTagValueAsync(tagValue, groups, CancellationToken.None);
        await broadcaster.SendEmulatorUpdatedAsync(emulator, groups, CancellationToken.None);
        await broadcaster.SendEventCreatedAsync(ev, groups, CancellationToken.None);

        client.Verify(runtimeClient => runtimeClient.TelemetryPoint(telemetry), Times.Once);
        client.Verify(runtimeClient => runtimeClient.TagValue(tagValue), Times.Once);
        client.Verify(runtimeClient => runtimeClient.EmulatorUpdated(emulator), Times.Once);
        client.Verify(runtimeClient => runtimeClient.EventCreated(ev), Times.Once);
    }

    private static RuntimeUpdatesHub CreateHub(string connectionId, IGroupManager groups)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(hubContext => hubContext.ConnectionId).Returns(connectionId);

        return new RuntimeUpdatesHub
        {
            Context = context.Object,
            Groups = groups,
        };
    }
}
