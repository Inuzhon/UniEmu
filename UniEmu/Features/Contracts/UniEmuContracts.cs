using System.Text.Json.Serialization;

namespace UniEmu.Features.Contracts;

public enum EmulatorStatus
{
    Running,
    Stopped,
    Error,
    Idle,
}

public enum TagType
{
    [JsonStringEnumMemberName("int")]
    Int,
    [JsonStringEnumMemberName("double")]
    Double,
    [JsonStringEnumMemberName("string")]
    String,
    [JsonStringEnumMemberName("bool")]
    Bool,
}

public enum TagSource
{
    [JsonStringEnumMemberName("static")]
    Static,
    [JsonStringEnumMemberName("formula")]
    Formula,
    [JsonStringEnumMemberName("script")]
    Script,
    [JsonStringEnumMemberName("generator")]
    Generator,
    [JsonStringEnumMemberName("cnc")]
    Cnc,
    [JsonStringEnumMemberName("scenario")]
    Scenario,
}

public enum TagTriggerMode
{
    [JsonStringEnumMemberName("once")]
    Once,
    [JsonStringEnumMemberName("cron")]
    Cron,
    [JsonStringEnumMemberName("interval")]
    Interval,
}

public enum TagTriggerEvent
{
    [JsonStringEnumMemberName("onStart")]
    OnStart,
    [JsonStringEnumMemberName("onStop")]
    OnStop,
}

public enum TagIntervalUnit
{
    [JsonStringEnumMemberName("ms")]
    Ms,
    [JsonStringEnumMemberName("sec")]
    Sec,
    [JsonStringEnumMemberName("min")]
    Min,
}

public enum CalcType
{
    None,
    Text,
    Line,
    Curve,
    Sequence,
    Random,
    Sinusoid,
    Square,
    Sawtooth,
    SquircleEarly,
    SquircleLate,
}

public enum ContinueOnFormulaEnd
{
    NoSignal,
    Zero,
    Repeat,
    Stretch,
}

public enum SpecialParameter
{
    None,
    PrgName,
    PartCounter,
    ErrorNum,
    FeedOvr,
    SpindleOvr,
    JogOvr,
    FrameNum,
    FrameText,
    ToolNum,
    WorkMode,
    SystemState,
    MachineReadiness,
    TechnologicalStop,
    EmergencyStop,
    FeedRate,
    ErrorText,
    CycleTime,
    SpindleSpeed,
    SpindleLoad,
    AxisLoad,
    AxisPosition,
    Message,
    CNCModel,
    FirmwareVersion,
    SerialNumber,
    PLCVersion,
    Subprogram,
    RapidOvr,
}

public enum ScriptScope
{
    [JsonStringEnumMemberName("shared")]
    Shared,
    [JsonStringEnumMemberName("emulator")]
    Emulator,
}

public enum CncScope
{
    [JsonStringEnumMemberName("shared")]
    Shared,
    [JsonStringEnumMemberName("emulator")]
    Emulator,
}

public enum EventLevel
{
    [JsonStringEnumMemberName("info")]
    Info,
    [JsonStringEnumMemberName("warn")]
    Warn,
    [JsonStringEnumMemberName("error")]
    Error,
    [JsonStringEnumMemberName("success")]
    Success,
}

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

public sealed record CreateEmulatorRequest(string Name, string TargetUrl, int IntervalSec);

public sealed record PatchEmulatorRequest(string? Name, string? TargetUrl, int? IntervalSec);

public sealed record PatchEmulatorStatusRequest(EmulatorStatus Status);

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
    SpecialParameter? SpecialParameter,
    string? Description);

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
    SpecialParameter? SpecialParameter,
    string? Description);

public sealed record CreateScriptRequest(string Name, ScriptScope Scope, string? EmulatorId);

public sealed record PatchScriptRequest(string? Name, string? Content);

public sealed record CreateCncProgramRequest(
    string Name,
    CncScope Scope,
    string? EmulatorId,
    string Content,
    int SizeBytes,
    bool? IsBinary,
    string? Description);

public sealed record PatchCncProgramRequest(
    string? Name,
    string? Description,
    string? Content);

public sealed record TelemetryIngestRequest(
    string EmulatorId,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, double> Values);

public sealed record PushEventRequest(
    string EmulatorId,
    string EmulatorName,
    EventLevel Level,
    string Message,
    DateTimeOffset Timestamp);
