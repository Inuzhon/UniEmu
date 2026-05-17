namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимое состояние CSX-скрипта между runtime-запусками.
/// </summary>
public sealed class ScriptRuntimeStateEntity
{
    /// <summary>Уникальный идентификатор записи состояния.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Идентификатор эмулятора, которому принадлежит состояние.</summary>
    public string EmulatorId { get; set; } = string.Empty;

    /// <summary>Ключ скрипта: inline-тег или файл скрипта.</summary>
    public string ScriptKey { get; set; } = string.Empty;

    /// <summary>JSON-снимок пользовательского состояния скрипта.</summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>Время последнего изменения состояния.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
