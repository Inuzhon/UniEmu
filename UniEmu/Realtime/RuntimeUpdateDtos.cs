using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

/// <summary>
/// Realtime-сообщение о новой точке телеметрии.
/// </summary>
/// <param name="EmulatorId">Идентификатор эмулятора.</param>
/// <param name="Point">Новая точка телеметрии.</param>
public sealed record RuntimeTelemetryUpdateDto(
    string EmulatorId,
    TelemetryPointDto Point);

/// <summary>
/// Realtime-сообщение об изменении значения тега.
/// </summary>
/// <param name="EmulatorId">Идентификатор эмулятора.</param>
/// <param name="TagId">Идентификатор тега.</param>
/// <param name="TagName">Отображаемое имя тега.</param>
/// <param name="Value">Новое значение тега.</param>
/// <param name="NumericValue">Новое значение, приведенное к числу, если это возможно.</param>
/// <param name="Timestamp">Время изменения значения.</param>
public sealed record RuntimeTagValueUpdateDto(
    string EmulatorId,
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);
