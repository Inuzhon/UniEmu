namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимый CSX-скрипт, доступный всем эмуляторам или одному эмулятору.
/// </summary>
public sealed class ScriptFileEntity
{
    /// <summary>Уникальный идентификатор скрипта.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Имя скрипта или путь, используемый в <c>#load</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Строковое значение <c>ScriptScope</c>.</summary>
    public string Scope { get; set; } = "shared";

    /// <summary>Идентификатор эмулятора для scoped-скрипта.</summary>
    public string? EmulatorId { get; set; }

    /// <summary>Текст CSX-скрипта.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Время последнего изменения скрипта.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Размер текста скрипта в байтах.</summary>
    public int SizeBytes { get; set; }
}
