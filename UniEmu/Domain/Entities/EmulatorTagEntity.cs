namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимая модель тега эмулятора с настройками источника, триггера и расчета.
/// </summary>
public sealed class EmulatorTagEntity
{
    /// <summary>Уникальный идентификатор тега.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Идентификатор эмулятора-владельца.</summary>
    public string EmulatorId { get; set; } = string.Empty;

    /// <summary>Навигационное свойство эмулятора-владельца.</summary>
    public EmulatorEntity? Emulator { get; set; }

    /// <summary>Отображаемое имя тега.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ключ тега во внешнем протоколе Dispatcher.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Строковое значение <c>TagType</c>.</summary>
    public string Type { get; set; } = "double";

    /// <summary>Строковое значение <c>TagSource</c>, определяющее способ расчета значения.</summary>
    public string Source { get; set; } = "static";

    /// <summary>Последнее сохраненное preview-значение тега.</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>JSON-конфигурация <c>TagTriggerDto</c>.</summary>
    public string TriggerJson { get; set; } = "{}";

    /// <summary>JSON-конфигурация генератора <c>TagCalcConfigDto</c>.</summary>
    public string? CalcJson { get; set; }

    /// <summary>JSON-конфигурация CSX-формулы <c>TagFormulaConfigDto</c>.</summary>
    public string? FormulaJson { get; set; }

    /// <summary>JSON-конфигурация сценария <c>TagScenarioConfigDto</c>.</summary>
    public string? ScenarioJson { get; set; }

    /// <summary>Признак участия тега в runtime-расчете и публикации.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Количество знаков округления для double-значений.</summary>
    public int? RoundDigits { get; set; }

    /// <summary>Строковое значение специального CNC-параметра, если тег связан с ним.</summary>
    public string? SpecialParameter { get; set; }

    /// <summary>Пользовательское описание тега.</summary>
    public string? Description { get; set; }
}
