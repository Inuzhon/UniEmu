namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимая точка телеметрии эмулятора.
/// </summary>
public sealed class TelemetryPointEntity
{
    /// <summary>Автоинкрементный идентификатор точки телеметрии.</summary>
    public long Id { get; set; }

    /// <summary>Идентификатор эмулятора, который сформировал точку.</summary>
    public string EmulatorId { get; set; } = string.Empty;

    /// <summary>Время формирования точки телеметрии.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>JSON-словарь значений тегов на момент публикации.</summary>
    public string ValuesJson { get; set; } = "{}";
}
