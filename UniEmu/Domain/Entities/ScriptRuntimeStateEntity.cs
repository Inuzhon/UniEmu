namespace UniEmu.Domain.Entities;

public sealed class ScriptRuntimeStateEntity
{
    public string Id { get; set; } = string.Empty;
    public string EmulatorId { get; set; } = string.Empty;
    public string ScriptKey { get; set; } = string.Empty;
    public string ValuesJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
