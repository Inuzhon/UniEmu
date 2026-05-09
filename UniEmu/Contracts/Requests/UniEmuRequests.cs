using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;

namespace UniEmu.Contracts.Requests;

public sealed record CreateEmulatorRequest(string Name, string TargetUrl, int IntervalSec, int ProtocolId);

public sealed record PatchEmulatorRequest(string? Name, string? TargetUrl, int? IntervalSec, int? ProtocolId);

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
    bool? Enabled,
    int? RoundDigits,
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
    bool? Enabled,
    int? RoundDigits,
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
