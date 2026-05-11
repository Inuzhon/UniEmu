namespace UniEmu.Scripting.Api;

public sealed class TagScriptGlobals
{
    /// <summary>
    /// Current tag calculation time in the application time zone.
    /// </summary>
    public DateTimeOffset Now { get; }

    public UniEmuScriptContext UniEmu { get; init; }

    public TagScriptGlobals(
        DateTimeOffset now,
        TagScriptValue tag,
        TagScriptTagAccessor tags,
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state)
    {
        Now = now;
        UniEmu = new UniEmuScriptContext(emulator, state, tag, tags);
    }
}

public sealed class UniEmuScriptContext
{
    public TagScriptEmulatorContext Emulator { get; }

    public TagScriptStateContext State { get; }

    /// <summary>
    /// Current tag information.
    /// </summary>
    public TagScriptValue Tag { get; }

    public TagScriptTagAccessor Tags { get; }

    public UniEmuScriptContext(
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state,
        TagScriptValue tag,
        TagScriptTagAccessor tags)
    {
        Emulator = emulator;
        State = state;
        Tag = tag;
        Tags = tags;
    }
}
