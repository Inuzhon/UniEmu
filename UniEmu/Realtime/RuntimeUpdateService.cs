using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

/// <summary>
/// Отправляет runtime-обновления подписчикам realtime-канала.
/// </summary>
public interface IRuntimeUpdateBroadcaster
{
    /// <summary>
    /// Отправляет обновление телеметрии в указанные группы.
    /// </summary>
    /// <param name="update">Данные обновления телеметрии.</param>
    /// <param name="groups">Группы получателей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет обновление значения тега в указанные группы.
    /// </summary>
    /// <param name="update">Данные обновления тега.</param>
    /// <param name="groups">Группы получателей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет обновление эмулятора в указанные группы.
    /// </summary>
    /// <param name="emulator">Обновленный эмулятор.</param>
    /// <param name="groups">Группы получателей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет новое системное событие в указанные группы.
    /// </summary>
    /// <param name="ev">Созданное системное событие.</param>
    /// <param name="groups">Группы получателей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken);
}

/// <summary>
/// Публикует runtime-обновления в общие и эмуляторные группы подписчиков.
/// </summary>
public sealed class RuntimeUpdateService(IRuntimeUpdateBroadcaster broadcaster)
{
    /// <summary>
    /// Имя общей группы, получающей все runtime-обновления.
    /// </summary>
    public const string AllGroup = "runtime:all";

    /// <summary>
    /// Возвращает имя группы обновлений конкретного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Имя SignalR-группы эмулятора.</returns>
    public static string EmulatorGroup(string emulatorId) => $"emulator:{emulatorId}";

    /// <summary>
    /// Публикует новую точку телеметрии.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="point">Точка телеметрии.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task PublishTelemetryAsync(string emulatorId, TelemetryPointDto point, CancellationToken cancellationToken)
    {
        return broadcaster.SendTelemetryAsync(
            new RuntimeTelemetryUpdateDto(emulatorId, point),
            GroupsForEmulator(emulatorId),
            cancellationToken);
    }

    /// <summary>
    /// Публикует изменение значения тега.
    /// </summary>
    /// <param name="update">Данные обновления тега.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task PublishTagValueAsync(RuntimeTagValueUpdateDto update, CancellationToken cancellationToken)
    {
        return broadcaster.SendTagValueAsync(update, GroupsForEmulator(update.EmulatorId), cancellationToken);
    }

    /// <summary>
    /// Публикует изменение эмулятора.
    /// </summary>
    /// <param name="emulator">Обновленный эмулятор.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task PublishEmulatorUpdatedAsync(EmulatorDto emulator, CancellationToken cancellationToken)
    {
        return broadcaster.SendEmulatorUpdatedAsync(emulator, GroupsForEmulator(emulator.Id), cancellationToken);
    }

    /// <summary>
    /// Публикует новое системное событие.
    /// </summary>
    /// <param name="ev">Созданное системное событие.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task PublishEventCreatedAsync(SystemEventDto ev, CancellationToken cancellationToken)
    {
        return broadcaster.SendEventCreatedAsync(ev, GroupsForEmulator(ev.EmulatorId), cancellationToken);
    }

    private static IReadOnlyList<string> GroupsForEmulator(string emulatorId) => [AllGroup, EmulatorGroup(emulatorId)];
}
