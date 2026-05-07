namespace UniEmu.Data;

public sealed class EmulatorEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Stopped";
    public string TargetUrl { get; set; } = string.Empty;
    public int IntervalSec { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public DateTimeOffset? NextRun { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public long TotalRequests { get; set; }
    public List<EmulatorTagEntity> Tags { get; set; } = [];
}

public sealed class EmulatorTagEntity
{
    public string Id { get; set; } = string.Empty;
    public string EmulatorId { get; set; } = string.Empty;
    public EmulatorEntity? Emulator { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = "double";
    public string Source { get; set; } = "static";
    public string Preview { get; set; } = string.Empty;
    public string TriggerJson { get; set; } = "{}";
    public string? CalcJson { get; set; }
    public string? FormulaJson { get; set; }
    public string? ScenarioJson { get; set; }
    public string? SpecialParameter { get; set; }
    public string? Description { get; set; }
}

public sealed class ScriptFileEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = "shared";
    public string? EmulatorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public int SizeBytes { get; set; }
}

public sealed class CncProgramEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = "shared";
    public string? EmulatorId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public bool? IsBinary { get; set; }
}

public sealed class TelemetryPointEntity
{
    public long Id { get; set; }
    public string EmulatorId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string ValuesJson { get; set; } = "{}";
}

public sealed class SystemEventEntity
{
    public string Id { get; set; } = string.Empty;
    public string EmulatorId { get; set; } = string.Empty;
    public string EmulatorName { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
