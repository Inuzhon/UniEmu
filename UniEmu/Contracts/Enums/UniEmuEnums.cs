using System.Text.Json.Serialization;

namespace UniEmu.Contracts.Enums;

/// <summary>
/// Рабочий статус эмулятора.
/// </summary>
public enum EmulatorStatus
{
    /// <summary>Эмулятор запущен и публикует данные.</summary>
    Running,

    /// <summary>Эмулятор остановлен.</summary>
    Stopped,

    /// <summary>Эмулятор находится в состоянии ошибки.</summary>
    Error,

    /// <summary>Эмулятор простаивает без активной публикации.</summary>
    Idle,
}

/// <summary>
/// Тип значения тега.
/// </summary>
public enum TagType
{
    /// <summary>Целочисленное значение.</summary>
    [JsonStringEnumMemberName("int")]
    Int,

    /// <summary>Число с плавающей точкой.</summary>
    [JsonStringEnumMemberName("double")]
    Double,

    /// <summary>Строковое значение.</summary>
    [JsonStringEnumMemberName("string")]
    String,

    /// <summary>Логическое значение.</summary>
    [JsonStringEnumMemberName("bool")]
    Bool,
}

/// <summary>
/// Источник значения тега.
/// </summary>
public enum TagSource
{
    /// <summary>Статическое значение.</summary>
    [JsonStringEnumMemberName("static")]
    Static,

    /// <summary>Значение, рассчитанное формулой.</summary>
    [JsonStringEnumMemberName("formula")]
    Formula,

    /// <summary>Значение, рассчитанное CSX-скриптом.</summary>
    [JsonStringEnumMemberName("script")]
    Script,

    /// <summary>Значение, рассчитанное генератором.</summary>
    [JsonStringEnumMemberName("generator")]
    Generator,

    /// <summary>Значение, рассчитанное генератором и затем обработанное CSX-скриптом.</summary>
    [JsonStringEnumMemberName("formulaScript")]
    FormulaScript,

    /// <summary>Значение, полученное из CNC-данных.</summary>
    [JsonStringEnumMemberName("cnc")]
    [Obsolete("Не использовать. Взять Static")]
    Cnc,

    /// <summary>Значение, рассчитанное сценарием генерации.</summary>
    [JsonStringEnumMemberName("scenario")]
    Scenario,
}

/// <summary>
/// Способ запуска расчета тега.
/// </summary>
public enum TagTriggerMode
{
    /// <summary>Однократный запуск.</summary>
    [JsonStringEnumMemberName("once")]
    Once,

    /// <summary>Запуск по cron-расписанию.</summary>
    [JsonStringEnumMemberName("cron")]
    Cron,

    /// <summary>Периодический запуск через заданный интервал.</summary>
    [JsonStringEnumMemberName("interval")]
    Interval,
}

/// <summary>
/// Событие эмулятора, запускающее расчет тега.
/// </summary>
public enum TagTriggerEvent
{
    /// <summary>Запуск эмулятора.</summary>
    [JsonStringEnumMemberName("onStart")]
    OnStart,

    /// <summary>Остановка эмулятора.</summary>
    [JsonStringEnumMemberName("onStop")]
    OnStop,
}

/// <summary>
/// Единица измерения периодического запуска тега.
/// </summary>
public enum TagIntervalUnit
{
    /// <summary>Миллисекунды.</summary>
    [JsonStringEnumMemberName("ms")]
    Ms,

    /// <summary>Секунды.</summary>
    [JsonStringEnumMemberName("sec")]
    Sec,

    /// <summary>Минуты.</summary>
    [JsonStringEnumMemberName("min")]
    Min,
}

/// <summary>
/// Тип генератора значения тега.
/// </summary>
public enum CalcType
{
    /// <summary>Генератор не используется.</summary>
    None,

    /// <summary>Статическое значение.</summary>
    Static,

    /// <summary>Линейное изменение значения.</summary>
    Line,

    /// <summary>Криволинейное изменение значения.</summary>
    Curve,

    /// <summary>Последовательность значений.</summary>
    Sequence,

    /// <summary>Случайное значение.</summary>
    Random,

    /// <summary>Синусоидальный сигнал.</summary>
    Sinusoid,

    /// <summary>Прямоугольный сигнал.</summary>
    Square,

    /// <summary>Пилообразный сигнал.</summary>
    Sawtooth,

    /// <summary>Раннее сглаженное изменение.</summary>
    SquircleEarly,

    /// <summary>Позднее сглаженное изменение.</summary>
    SquircleLate,
}

/// <summary>
/// Поведение сценарного тега после завершения формулы или последнего участка.
/// </summary>
public enum ContinueOnFormulaEnd
{
    /// <summary>Не выдавать сигнал.</summary>
    NoSignal,

    /// <summary>Выдавать нулевое значение.</summary>
    Zero,

    /// <summary>Повторять сценарий.</summary>
    Repeat,

    /// <summary>Растягивать последнее значение.</summary>
    Stretch,
}

/// <summary>
/// Специальный параметр диспетчерского протокола.
/// </summary>
public enum SpecialParameter
{
    /// <summary>Специальный параметр не задан.</summary>
    None = 0,

    /// <summary>Имя программы.</summary>
    PrgName = 1,

    /// <summary>Счетчик деталей.</summary>
    PartCounter = 2,

    /// <summary>Номер ошибки.</summary>
    ErrorNum = 3,

    /// <summary>Коррекция подачи.</summary>
    FeedOvr = 4,

    /// <summary>Коррекция шпинделя.</summary>
    SpindleOvr = 5,

    /// <summary>Коррекция ручного перемещения.</summary>
    JogOvr = 6,

    /// <summary>Номер кадра.</summary>
    FrameNum = 7,

    /// <summary>Текст кадра.</summary>
    FrameText = 8,

    /// <summary>Номер инструмента.</summary>
    ToolNum = 9,

    /// <summary>Режим работы.</summary>
    WorkMode = 10,

    /// <summary>Состояние системы.</summary>
    SystemState = 11,

    /// <summary>Готовность станка.</summary>
    MachineReadiness = 12,

    /// <summary>Технологический останов.</summary>
    TechnologicalStop = 13,

    /// <summary>Аварийный останов.</summary>
    EmergencyStop = 14,

    /// <summary>Скорость подачи.</summary>
    FeedRate = 15,

    /// <summary>Текст ошибки.</summary>
    ErrorText = 16,

    /// <summary>Время цикла.</summary>
    CycleTime = 17,

    /// <summary>Скорость шпинделя.</summary>
    SpindleSpeed = 18,

    /// <summary>Нагрузка шпинделя.</summary>
    SpindleLoad = 19,

    /// <summary>Нагрузка оси.</summary>
    AxisLoad = 20,

    /// <summary>Позиция оси.</summary>
    AxisPosition = 21,

    /// <summary>Сообщение системы ЧПУ.</summary>
    Message = 22,

    /// <summary>Модель ЧПУ.</summary>
    // ReSharper disable once InconsistentNaming
    CNCModel = 23,

    /// <summary>Версия прошивки.</summary>
    FirmwareVersion = 24,

    /// <summary>Серийный номер.</summary>
    SerialNumber = 25,

    /// <summary>Версия PLC.</summary>
    // ReSharper disable once InconsistentNaming
    PLCVersion = 26,

    /// <summary>Подпрограмма.</summary>
    Subprogram = 27,
}

/// <summary>
/// Область видимости CSX-скрипта.
/// </summary>
public enum ScriptScope
{
    /// <summary>Скрипт доступен всем эмуляторам.</summary>
    [JsonStringEnumMemberName("shared")]
    Shared,

    /// <summary>Скрипт доступен только одному эмулятору.</summary>
    [JsonStringEnumMemberName("emulator")]
    Emulator,
}

/// <summary>
/// Область видимости CNC-программы.
/// </summary>
public enum CncScope
{
    /// <summary>Программа доступна всем эмуляторам.</summary>
    [JsonStringEnumMemberName("shared")]
    Shared,

    /// <summary>Программа доступна только одному эмулятору.</summary>
    [JsonStringEnumMemberName("emulator")]
    Emulator,
}

/// <summary>
/// Уровень системного события.
/// </summary>
public enum EventLevel
{
    /// <summary>Информационное событие.</summary>
    [JsonStringEnumMemberName("info")]
    Info,

    /// <summary>Предупреждение.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn,

    /// <summary>Ошибка.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>Успешное действие.</summary>
    [JsonStringEnumMemberName("success")]
    Success,
}
