namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимое системное событие эмулятора.
/// </summary>
public sealed class SystemEventEntity
{
    /// <summary>Уникальный идентификатор события.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Идентификатор эмулятора, к которому относится событие.</summary>
    public string EmulatorId { get; set; } = string.Empty;

    /// <summary>Имя эмулятора на момент создания события.</summary>
    public string EmulatorName { get; set; } = string.Empty;

    /// <summary>Строковое значение <c>EventLevel</c>.</summary>
    public string Level { get; set; } = "info";

    /// <summary>Текст события.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Время возникновения события.</summary>
    public DateTimeOffset Timestamp { get; set; }
}
