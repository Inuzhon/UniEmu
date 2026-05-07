namespace UniEmu.Domain.Entities;

public sealed class SystemEventEntity
{
    public string Id { get; set; } = string.Empty;
    public string EmulatorId { get; set; } = string.Empty;
    public string EmulatorName { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
