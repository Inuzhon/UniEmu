namespace UniEmu.Domain.Entities;

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
