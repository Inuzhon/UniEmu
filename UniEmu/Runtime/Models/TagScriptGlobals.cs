namespace UniEmu.Runtime.Models;

public sealed class TagScriptGlobals(
    TagScriptTagAccessor tag,
    DateTimeOffset now,
    TagScriptEmulatorContext emulator,
    TagScriptStateContext state)
{
    public TagScriptTagAccessor Tag { get; } = tag;
    public TagScriptTagAccessor Tags { get; } = tag;
    public DateTimeOffset Now { get; } = now;
    public TagScriptEmulatorContext Emulator { get; } = emulator;
    public TagScriptStateContext State { get; } = state;

    public double Random(double min, double max) => System.Random.Shared.NextDouble() * (max - min) + min;

    public void Log(string message) { }

    public void LogWarn(string message) { }

    public void LogError(string message) { }
}
