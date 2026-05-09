using Microsoft.AspNetCore.SignalR;
using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

public interface IRuntimeUpdatesClient
{
    Task TelemetryPoint(RuntimeTelemetryUpdateDto update);

    Task TagValue(RuntimeTagValueUpdateDto update);

    Task EmulatorUpdated(EmulatorDto emulator);

    Task EventCreated(SystemEventDto ev);
}

public sealed class RuntimeUpdatesHub : Hub<IRuntimeUpdatesClient>
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.AllGroup);
        await base.OnConnectedAsync();
    }

    public Task SubscribeAll()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.AllGroup);
    }

    public Task SubscribeEmulator(string emulatorId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.EmulatorGroup(emulatorId));
    }

    public Task UnsubscribeEmulator(string emulatorId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, RuntimeUpdateService.EmulatorGroup(emulatorId));
    }
}

public sealed class SignalRRuntimeUpdateBroadcaster(
    IHubContext<RuntimeUpdatesHub, IRuntimeUpdatesClient> hubContext) : IRuntimeUpdateBroadcaster
{
    public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).TelemetryPoint(update);
    }

    public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).TagValue(update);
    }

    public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).EmulatorUpdated(emulator);
    }

    public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).EventCreated(ev);
    }
}
