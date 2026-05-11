namespace UniEmu.Runtime.Scripting.UserScripts;

public sealed class TagScriptGlobals
{
    /// <summary>
    /// Время вычисления тега
    /// </summary>
    public DateTimeOffset Now { get; }

    public UniEmu UniEmu { get; init; }

    public TagScriptGlobals(
        DateTimeOffset now,
        TagScriptValue tag,
        TagScriptTagAccessor tags,
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state
    )
    {
        Now = now;
        UniEmu = new(emulator, state, tag, tags);
    }
}

public sealed class UniEmu
{
    public TagScriptEmulatorContext Emulator { get; }

    public TagScriptStateContext State { get; }

    /// <summary>
    /// Информация о текущем теге
    /// </summary>
    public TagScriptValue Tag { get; }

    public TagScriptTagAccessor Tags { get; }


    public UniEmu(
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state,
        TagScriptValue tag,
        TagScriptTagAccessor tags
    )
    {
        Emulator = emulator;
        State = state;
        Tag = tag;
        Tags = tags;
    }
}
