namespace UniEmu.Domain.Entities;

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
