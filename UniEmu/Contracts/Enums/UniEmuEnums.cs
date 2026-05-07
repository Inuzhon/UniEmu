using System.Text.Json.Serialization;

namespace UniEmu.Contracts.Enums;

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
