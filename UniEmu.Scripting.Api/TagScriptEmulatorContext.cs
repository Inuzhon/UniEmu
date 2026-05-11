namespace UniEmu.Scripting.Api;

/// <summary>
/// Информация об эмуляторе, доступная скрипту тега.
/// </summary>
/// <param name="Id">Идентификатор эмулятора.</param>
/// <param name="Name">Отображаемое имя эмулятора.</param>
/// <param name="Status">Текущий статус эмулятора.</param>
/// <param name="StartTime">Время запуска эмулятора, если оно известно.</param>
public sealed record TagScriptEmulatorContext(string Id, string Name, string Status, DateTimeOffset? StartTime);
