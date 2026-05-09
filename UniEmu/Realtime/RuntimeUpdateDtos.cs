using UniEmu.Contracts.Dtos;

namespace UniEmu.Realtime;

public sealed record RuntimeTelemetryUpdateDto(
    string EmulatorId,
    TelemetryPointDto Point);

public sealed record RuntimeTagValueUpdateDto(
    string EmulatorId,
    string TagId,
    string TagName,
    object? Value,
    double? NumericValue,
    DateTimeOffset Timestamp);
