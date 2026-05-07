namespace UniEmu.Domain.Entities;

public sealed class TelemetryPointEntity
{
    public long Id { get; set; }
    public string EmulatorId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string ValuesJson { get; set; } = "{}";
}
