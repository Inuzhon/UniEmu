namespace UniEmu.Domain.Entities;

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
