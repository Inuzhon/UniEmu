namespace UniEmu.Domain.Entities;

/// <summary>
/// Хранимая CNC-программа, доступная всем эмуляторам или одному эмулятору.
/// </summary>
public sealed class CncProgramEntity
{
    /// <summary>Уникальный идентификатор программы.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Имя файла CNC-программы.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Строковое значение <c>CncScope</c>.</summary>
    public string Scope { get; set; } = "shared";

    /// <summary>Идентификатор эмулятора для scoped-программы.</summary>
    public string? EmulatorId { get; set; }

    /// <summary>Пользовательское описание программы.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Текстовое содержимое программы.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Размер программы в байтах.</summary>
    public int SizeBytes { get; set; }

    /// <summary>Время последнего изменения программы.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Время загрузки программы в хранилище UniEmu.</summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>Признак бинарного содержимого, если он известен.</summary>
    public bool? IsBinary { get; set; }
}
