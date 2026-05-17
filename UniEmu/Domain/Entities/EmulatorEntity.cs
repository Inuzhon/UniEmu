namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимая модель эмулятора, его подключения к Dispatcher и runtime-статистики.
/// </summary>
public sealed class EmulatorEntity
{
    /// <summary>Уникальный идентификатор эмулятора.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Отображаемое имя эмулятора.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Строковое значение <c>EmulatorStatus</c>, сохраненное для совместимости с JSON/API enum.</summary>
    public string Status { get; set; } = "Stopped";

    /// <summary>Идентификатор станка или протокола во внешнем Dispatcher.</summary>
    public int ProtocolId { get; set; }

    /// <summary>Базовый URL Dispatcher или совместимого endpoint-а публикации.</summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Интервал публикации телеметрии в секундах.</summary>
    public int IntervalSec { get; set; }

    /// <summary>Время последнего publish-цикла.</summary>
    public DateTimeOffset? LastRun { get; set; }

    /// <summary>Ожидаемое время следующего publish-цикла.</summary>
    public DateTimeOffset? NextRun { get; set; }

    /// <summary>Последняя runtime-ошибка эмулятора, если он был переведен в ошибочное состояние.</summary>
    public string? LastError { get; set; }

    /// <summary>Время перехода эмулятора в состояние запуска.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Счетчик успешных publish-циклов.</summary>
    public long TotalRequests { get; set; }

    /// <summary>Теги, принадлежащие эмулятору.</summary>
    public List<EmulatorTagEntity> Tags { get; set; } = [];
}
