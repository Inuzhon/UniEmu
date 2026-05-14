using Microsoft.AspNetCore.SignalR;
using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

/// <summary>
/// Клиентский контракт SignalR для runtime-обновлений UniEmu.
/// </summary>
public interface IRuntimeUpdatesClient
{
    /// <summary>
    /// Получает новую точку телеметрии.
    /// </summary>
    /// <param name="update">Данные обновления телеметрии.</param>
    Task TelemetryPoint(RuntimeTelemetryUpdateDto update);

    /// <summary>
    /// Получает новое значение тега.
    /// </summary>
    /// <param name="update">Данные обновления тега.</param>
    Task TagValue(RuntimeTagValueUpdateDto update);

    /// <summary>
    /// Получает обновленное состояние эмулятора.
    /// </summary>
    /// <param name="emulator">Обновленный эмулятор.</param>
    Task EmulatorUpdated(EmulatorDto emulator);

    /// <summary>
    /// Получает новое системное событие.
    /// </summary>
    /// <param name="ev">Созданное событие.</param>
    Task EventCreated(SystemEventDto ev);
}

/// <summary>
/// SignalR hub для подписки на runtime-обновления.
/// </summary>
public sealed class RuntimeUpdatesHub : Hub<IRuntimeUpdatesClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.AllGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Подписывает текущее подключение на все runtime-обновления.
    /// </summary>
    public Task SubscribeAll()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.AllGroup);
    }

    /// <summary>
    /// Подписывает текущее подключение на обновления конкретного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    public Task SubscribeEmulator(string emulatorId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeUpdateService.EmulatorGroup(emulatorId));
    }

    /// <summary>
    /// Отписывает текущее подключение от обновлений конкретного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    public Task UnsubscribeEmulator(string emulatorId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, RuntimeUpdateService.EmulatorGroup(emulatorId));
    }
}

/// <summary>
/// Реализация отправки runtime-обновлений через SignalR.
/// </summary>
public sealed class SignalRRuntimeUpdateBroadcaster(
    IHubContext<RuntimeUpdatesHub, IRuntimeUpdatesClient> hubContext) : IRuntimeUpdateBroadcaster
{
    /// <inheritdoc />
    public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).TelemetryPoint(update);
    }

    /// <inheritdoc />
    public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).TagValue(update);
    }

    /// <inheritdoc />
    public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).EmulatorUpdated(emulator);
    }

    /// <inheritdoc />
    public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Groups(groups).EventCreated(ev);
    }
}
