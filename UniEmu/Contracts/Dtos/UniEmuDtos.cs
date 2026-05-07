using UniEmu.Contracts.Enums;

namespace UniEmu.Contracts.Dtos;

public sealed record EmulatorDto(
    string Id,
    string Name,
    EmulatorStatus Status,
    string TargetUrl,
    int IntervalSec,
    DateTimeOffset? LastRun,
    DateTimeOffset? NextRun,
    string? LastError,
    int TagsCount,
    long UptimeSec,
    long TotalRequests);

public sealed record TagTriggerDto(
    TagTriggerMode Mode,
    TagTriggerEvent? Event,
    string? Cron,
    int? IntervalValue,
    TagIntervalUnit? IntervalUnit);

public sealed record TagCalcConfigDto(
    CalcType Type,
    string? Start,
    string? Finish,
    int? Duration,
    double? Amplitude,
    double? Period,
    double? Curvature,
    double? Distortion);

public sealed record TagFormulaConfigDto(string? ScriptId, string? InlineScript);

public sealed record TagScenarioSegmentDto(
    string Id,
    double Duration,
    TagCalcConfigDto Calc,
    string? Label);

public sealed record TagScenarioConfigDto(
    IReadOnlyList<TagScenarioSegmentDto> Segments,
    ContinueOnFormulaEnd ContinueOnFormulaEnd,
    string? StartValue);

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
    SpecialParameter? SpecialParameter,
    string? Description);

public sealed record TelemetryPointDto(
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, double> Values);

public sealed record SystemEventDto(
    string Id,
    string EmulatorId,
    string EmulatorName,
    EventLevel Level,
    string Message,
    DateTimeOffset Timestamp);

public sealed record ScriptFileDto(
    string Id,
    string Name,
    ScriptScope Scope,
    string? EmulatorId,
    string Content,
    DateTimeOffset UpdatedAt,
    int SizeBytes);

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
