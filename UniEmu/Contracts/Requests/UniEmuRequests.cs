using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;

namespace UniEmu.Contracts.Requests;

/// <summary>
/// Запрос на создание эмулятора.
/// </summary>
/// <param name="Name">Отображаемое имя эмулятора.</param>
/// <param name="TargetUrl">Адрес целевой системы или диспетчера.</param>
/// <param name="IntervalSec">Интервал публикации телеметрии в секундах.</param>
/// <param name="ProtocolId">Идентификатор протокола обмена.</param>
public sealed record CreateEmulatorRequest(string Name, string TargetUrl, int IntervalSec, int ProtocolId);

/// <summary>
/// Запрос на частичное обновление настроек эмулятора.
/// </summary>
/// <param name="Name">Новое отображаемое имя эмулятора.</param>
/// <param name="TargetUrl">Новый адрес целевой системы или диспетчера.</param>
/// <param name="IntervalSec">Новый интервал публикации телеметрии в секундах.</param>
/// <param name="ProtocolId">Новый идентификатор протокола обмена.</param>
public sealed record PatchEmulatorRequest(string? Name, string? TargetUrl, int? IntervalSec, int? ProtocolId);

/// <summary>
/// Запрос на изменение статуса эмулятора.
/// </summary>
/// <param name="Status">Новый статус эмулятора.</param>
public sealed record PatchEmulatorStatusRequest(EmulatorStatus Status);

/// <summary>
/// Запрос на создание тега эмулятора.
/// </summary>
/// <param name="Name">Отображаемое имя тега.</param>
/// <param name="Key">Уникальный ключ тега для телеметрии и скриптов.</param>
/// <param name="Type">Тип значения тега.</param>
/// <param name="Source">Источник значения тега.</param>
/// <param name="Preview">Начальное или предварительное значение.</param>
/// <param name="Trigger">Настройки запуска расчета тега.</param>
/// <param name="Calc">Настройки генератора значения.</param>
/// <param name="Formula">Настройки формулы или CSX-скрипта.</param>
/// <param name="Scenario">Настройки сценарной генерации.</param>
/// <param name="Enabled">Признак включения тега после создания.</param>
/// <param name="RoundDigits">Количество знаков округления для числовых значений.</param>
/// <param name="SpecialParameter">Специальный параметр диспетчерского протокола.</param>
/// <param name="Description">Пользовательское описание тега.</param>
public sealed record CreateTagRequest(
    string Name,
    string Key,
    TagType Type,
    TagSource Source,
    string Preview,
    TagTriggerDto Trigger,
    TagCalcConfigDto? Calc,
    TagFormulaConfigDto? Formula,
    TagScenarioConfigDto? Scenario,
    bool? Enabled,
    int? RoundDigits,
    SpecialParameter? SpecialParameter,
    string? Description);

/// <summary>
/// Запрос на полную замену конфигурации тега.
/// </summary>
/// <param name="Name">Новое отображаемое имя тега.</param>
/// <param name="Key">Новый уникальный ключ тега.</param>
/// <param name="Type">Тип значения тега.</param>
/// <param name="Source">Источник значения тега.</param>
/// <param name="Preview">Предварительное или последнее значение тега.</param>
/// <param name="Trigger">Настройки запуска расчета тега.</param>
/// <param name="Calc">Настройки генератора значения.</param>
/// <param name="Formula">Настройки формулы или CSX-скрипта.</param>
/// <param name="Scenario">Настройки сценарной генерации.</param>
/// <param name="Enabled">Признак включения тега.</param>
/// <param name="RoundDigits">Количество знаков округления для числовых значений.</param>
/// <param name="SpecialParameter">Специальный параметр диспетчерского протокола.</param>
/// <param name="Description">Пользовательское описание тега.</param>
public sealed record ReplaceTagRequest(
    string Name,
    string Key,
    TagType Type,
    TagSource Source,
    string Preview,
    TagTriggerDto Trigger,
    TagCalcConfigDto? Calc,
    TagFormulaConfigDto? Formula,
    TagScenarioConfigDto? Scenario,
    bool? Enabled,
    int? RoundDigits,
    SpecialParameter? SpecialParameter,
    string? Description);

/// <summary>
/// Запрос на создание CSX-скрипта.
/// </summary>
/// <param name="Name">Имя файла скрипта.</param>
/// <param name="Scope">Область видимости скрипта.</param>
/// <param name="EmulatorId">Идентификатор эмулятора для эмуляторного скрипта.</param>
public sealed record CreateScriptRequest(string Name, ScriptScope Scope, string? EmulatorId);

/// <summary>
/// Запрос на обновление CSX-скрипта.
/// </summary>
/// <param name="Name">Новое имя файла скрипта.</param>
/// <param name="Content">Новое содержимое CSX-скрипта.</param>
public sealed record PatchScriptRequest(string? Name, string? Content);

/// <summary>
/// Запрос на создание CNC-программы.
/// </summary>
/// <param name="Name">Имя программы.</param>
/// <param name="Scope">Область видимости программы.</param>
/// <param name="EmulatorId">Идентификатор эмулятора для эмуляторной программы.</param>
/// <param name="Content">Содержимое программы.</param>
/// <param name="SizeBytes">Размер содержимого в байтах.</param>
/// <param name="IsBinary">Признак бинарного содержимого, если он известен.</param>
/// <param name="Description">Описание программы.</param>
public sealed record CreateCncProgramRequest(
    string Name,
    CncScope Scope,
    string? EmulatorId,
    string Content,
    int SizeBytes,
    bool? IsBinary,
    string? Description);

/// <summary>
/// Запрос на обновление CNC-программы.
/// </summary>
/// <param name="Name">Новое имя программы.</param>
/// <param name="Description">Новое описание программы.</param>
/// <param name="Content">Новое содержимое программы.</param>
public sealed record PatchCncProgramRequest(
    string? Name,
    string? Description,
    string? Content);

/// <summary>
/// Запрос на ручную запись точки телеметрии.
/// </summary>
/// <param name="EmulatorId">Идентификатор эмулятора.</param>
/// <param name="Timestamp">Время точки телеметрии.</param>
/// <param name="Values">Значения тегов по их ключам.</param>
public sealed record TelemetryIngestRequest(
    string EmulatorId,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// Запрос на публикацию системного события.
/// </summary>
/// <param name="EmulatorId">Идентификатор связанного эмулятора.</param>
/// <param name="EmulatorName">Имя связанного эмулятора.</param>
/// <param name="Level">Уровень события.</param>
/// <param name="Message">Текст события.</param>
/// <param name="Timestamp">Время события.</param>
public sealed record PushEventRequest(
    string EmulatorId,
    string EmulatorName,
    EventLevel Level,
    string Message,
    DateTimeOffset Timestamp);
