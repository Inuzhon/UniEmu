using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

public interface IRuntimeUpdateBroadcaster
{
    Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken);
}

public sealed class RuntimeUpdateService(IRuntimeUpdateBroadcaster broadcaster)
{
    public const string AllGroup = "runtime:all";

    public static string EmulatorGroup(string emulatorId) => $"emulator:{emulatorId}";

    public Task PublishTelemetryAsync(string emulatorId, TelemetryPointDto point, CancellationToken cancellationToken)
    {
        return broadcaster.SendTelemetryAsync(
            new RuntimeTelemetryUpdateDto(emulatorId, point),
            GroupsForEmulator(emulatorId),
            cancellationToken);
    }

    public Task PublishTagValueAsync(RuntimeTagValueUpdateDto update, CancellationToken cancellationToken)
    {
        return broadcaster.SendTagValueAsync(update, GroupsForEmulator(update.EmulatorId), cancellationToken);
    }

    public Task PublishEmulatorUpdatedAsync(EmulatorDto emulator, CancellationToken cancellationToken)
    {
        return broadcaster.SendEmulatorUpdatedAsync(emulator, GroupsForEmulator(emulator.Id), cancellationToken);
    }

    public Task PublishEventCreatedAsync(SystemEventDto ev, CancellationToken cancellationToken)
    {
        return broadcaster.SendEventCreatedAsync(ev, GroupsForEmulator(ev.EmulatorId), cancellationToken);
    }

    private static IReadOnlyList<string> GroupsForEmulator(string emulatorId) => [AllGroup, EmulatorGroup(emulatorId)];
}
