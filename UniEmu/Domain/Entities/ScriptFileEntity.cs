namespace UniEmu.Domain.Entities;

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
