using UniEmu.Contracts.Enums;

namespace UniEmu.Contracts.Dtos;

/// <summary>
/// Снимок эмулятора, возвращаемый клиентскому приложению.
/// </summary>
/// <param name="Id">Идентификатор эмулятора.</param>
/// <param name="Name">Отображаемое имя эмулятора.</param>
/// <param name="Status">Текущий статус эмулятора.</param>
/// <param name="ProtocolId">Идентификатор протокола обмена с целевой системой.</param>
/// <param name="TargetUrl">Адрес целевой системы или диспетчера.</param>
/// <param name="IntervalSec">Интервал публикации телеметрии в секундах.</param>
/// <param name="LastRun">Время последнего цикла публикации.</param>
/// <param name="NextRun">Планируемое время следующего цикла публикации.</param>
/// <param name="LastError">Текст последней ошибки выполнения.</param>
/// <param name="TagsCount">Количество тегов, настроенных для эмулятора.</param>
/// <param name="UptimeSec">Время работы эмулятора в секундах.</param>
/// <param name="TotalRequests">Общее количество выполненных запросов публикации.</param>
public sealed record EmulatorDto(
    string Id,
    string Name,
    EmulatorStatus Status,
    int ProtocolId,
    string TargetUrl,
    int IntervalSec,
    DateTimeOffset? LastRun,
    DateTimeOffset? NextRun,
    string? LastError,
    int TagsCount,
    long UptimeSec,
    long TotalRequests);

/// <summary>
/// Настройки события или расписания, запускающего расчет тега.
/// </summary>
/// <param name="Mode">Режим запуска тега.</param>
/// <param name="Event">Событие эмулятора для событийного режима.</param>
/// <param name="Cron">Cron-выражение для запуска по расписанию.</param>
/// <param name="IntervalValue">Значение периодического интервала.</param>
/// <param name="IntervalUnit">Единица измерения периодического интервала.</param>
public sealed record TagTriggerDto(
    TagTriggerMode Mode,
    TagTriggerEvent? Event,
    string? Cron,
    int? IntervalValue,
    TagIntervalUnit? IntervalUnit);

/// <summary>
/// Параметры генератора значения тега.
/// </summary>
/// <param name="Type">Тип генератора.</param>
/// <param name="Start">Начальное значение или начало последовательности.</param>
/// <param name="Finish">Конечное значение или конец последовательности.</param>
/// <param name="Duration">Длительность участка генерации.</param>
/// <param name="Amplitude">Амплитуда периодического сигнала.</param>
/// <param name="Period">Период периодического сигнала.</param>
/// <param name="Curvature">Коэффициент кривизны для нелинейных генераторов.</param>
/// <param name="Distortion">Коэффициент искажения формы сигнала.</param>
public sealed record TagCalcConfigDto(
    CalcType Type,
    string? Start,
    string? Finish,
    int? Duration,
    double? Amplitude,
    double? Period,
    double? Curvature,
    double? Distortion);

/// <summary>
/// Настройки формульного или скриптового расчета тега.
/// </summary>
/// <param name="ScriptId">Идентификатор сохраненного CSX-скрипта.</param>
/// <param name="InlineScript">Текст встроенного CSX-скрипта.</param>
public sealed record TagFormulaConfigDto(string? ScriptId, string? InlineScript);

/// <summary>
/// Один участок сценария генерации значения тега.
/// </summary>
/// <param name="Id">Идентификатор участка сценария.</param>
/// <param name="Duration">Длительность участка.</param>
/// <param name="Calc">Конфигурация генератора для участка.</param>
/// <param name="Label">Пользовательская подпись участка.</param>
public sealed record TagScenarioSegmentDto(
    string Id,
    double Duration,
    TagCalcConfigDto Calc,
    string? Label);

/// <summary>
/// Последовательность участков сценарного тега.
/// </summary>
/// <param name="Segments">Участки сценария в порядке выполнения.</param>
/// <param name="ContinueOnFormulaEnd">Поведение после завершения последнего участка.</param>
/// <param name="StartValue">Начальное значение сценария.</param>
public sealed record TagScenarioConfigDto(
    IReadOnlyList<TagScenarioSegmentDto> Segments,
    ContinueOnFormulaEnd ContinueOnFormulaEnd,
    string? StartValue);

/// <summary>
/// Снимок тега эмулятора и его конфигурации.
/// </summary>
/// <param name="Id">Идентификатор тега.</param>
/// <param name="Name">Отображаемое имя тега.</param>
/// <param name="Key">Уникальный ключ тега для телеметрии и скриптов.</param>
/// <param name="Type">Тип значения тега.</param>
/// <param name="Source">Источник значения тега.</param>
/// <param name="Preview">Последнее известное или начальное значение в строковом виде.</param>
/// <param name="Trigger">Настройки запуска расчета тега.</param>
/// <param name="Calc">Настройки генератора значения.</param>
/// <param name="Formula">Настройки формулы или CSX-скрипта.</param>
/// <param name="Scenario">Настройки сценарной генерации.</param>
/// <param name="Enabled">Признак участия тега в публикации телеметрии.</param>
/// <param name="RoundDigits">Количество знаков округления для числовых значений.</param>
/// <param name="SpecialParameter">Специальный параметр диспетчерского протокола.</param>
/// <param name="Description">Пользовательское описание тега.</param>
public sealed record EmulatorTagDto(
    string Id,
    string Name,
    string Key,
    TagType Type,
    TagSource Source,
    string Preview,
    TagTriggerDto Trigger,
    TagCalcConfigDto? Calc,
    TagFormulaConfigDto? Formula,
    TagScenarioConfigDto? Scenario,
    bool Enabled,
    int? RoundDigits,
    SpecialParameter? SpecialParameter,
    string? Description);

/// <summary>
/// Точка телеметрии эмулятора.
/// </summary>
/// <param name="Timestamp">Время получения или публикации точки.</param>
/// <param name="Values">Значения тегов по их ключам.</param>
public sealed record TelemetryPointDto(
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// Системное событие, связанное с эмулятором или его процессами.
/// </summary>
/// <param name="Id">Идентификатор события.</param>
/// <param name="EmulatorId">Идентификатор связанного эмулятора.</param>
/// <param name="EmulatorName">Имя связанного эмулятора на момент события.</param>
/// <param name="Level">Уровень важности события.</param>
/// <param name="Message">Текст события.</param>
/// <param name="Timestamp">Время события.</param>
public sealed record SystemEventDto(
    string Id,
    string EmulatorId,
    string EmulatorName,
    EventLevel Level,
    string Message,
    DateTimeOffset Timestamp);

/// <summary>
/// CSX-скрипт, доступный тегам.
/// </summary>
/// <param name="Id">Идентификатор скрипта.</param>
/// <param name="Name">Имя файла скрипта.</param>
/// <param name="Scope">Область видимости скрипта.</param>
/// <param name="EmulatorId">Идентификатор эмулятора для эмуляторного скрипта.</param>
/// <param name="Content">Текст CSX-скрипта.</param>
/// <param name="UpdatedAt">Время последнего изменения.</param>
/// <param name="SizeBytes">Размер содержимого в байтах.</param>
public sealed record ScriptFileDto(
    string Id,
    string Name,
    ScriptScope Scope,
    string? EmulatorId,
    string Content,
    DateTimeOffset UpdatedAt,
    int SizeBytes);

/// <summary>
/// CNC-программа, сохраненная в хранилище UniEmu.
/// </summary>
/// <param name="Id">Идентификатор программы.</param>
/// <param name="Name">Имя программы.</param>
/// <param name="Scope">Область видимости программы.</param>
/// <param name="EmulatorId">Идентификатор эмулятора для эмуляторной программы.</param>
/// <param name="Description">Описание программы.</param>
/// <param name="Content">Содержимое программы.</param>
/// <param name="SizeBytes">Размер содержимого в байтах.</param>
/// <param name="UpdatedAt">Время последнего изменения.</param>
/// <param name="UploadedAt">Время первичной загрузки.</param>
/// <param name="IsBinary">Признак бинарного содержимого, если он известен.</param>
public sealed record CncProgramDto(
    string Id,
    string Name,
    CncScope Scope,
    string? EmulatorId,
    string Description,
    string Content,
    int SizeBytes,
    DateTimeOffset UpdatedAt,
    DateTimeOffset UploadedAt,
    bool? IsBinary);
