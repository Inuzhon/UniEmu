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
    None = 0,
    PrgName = 1,
    PartCounter = 2,
    ErrorNum = 3,
    FeedOvr = 4,
    SpindleOvr = 5,
    JogOvr = 6,
    FrameNum = 7,
    FrameText = 8,
    ToolNum = 9,
    WorkMode = 10,
    SystemState = 11,
    MachineReadiness = 12,
    TechnologicalStop = 13,
    EmergencyStop = 14,
    FeedRate = 15,
    ErrorText = 16,
    CycleTime = 17,
    SpindleSpeed = 18,
    SpindleLoad = 19,
    AxisLoad = 20,
    AxisPosition = 21,
    Message = 22,
    CNCModel = 23,
    FirmwareVersion = 24,
    SerialNumber = 25,
    PLCVersion = 26,
    Subprogram = 27,
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
