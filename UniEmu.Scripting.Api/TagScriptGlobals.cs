namespace UniEmu.Scripting.Api;

/// <summary>
/// Глобальные объекты, доступные скрипту при вычислении тега.
/// </summary>
[ScriptingApi]
public sealed class TagScriptGlobals
{
    /// <summary>
    /// Текущее время вычисления тега в часовом поясе приложения.
    /// </summary>
    [ScriptingApi]
    public DateTimeOffset Now { get; }

    /// <summary>
    /// Контекст UniEmu с данными эмулятора, текущего тега, других тегов и состояния скрипта.
    /// </summary>
    [ScriptingApi]
    public UniEmuScriptContext UniEmu { get; init; }

    /// <summary>
    /// Создает набор глобальных объектов для выполнения скрипта тега.
    /// </summary>
    /// <param name="now">Текущее время вычисления тега.</param>
    /// <param name="tag">Текущий вычисляемый тег.</param>
    /// <param name="tags">Доступ к значениям других тегов.</param>
    /// <param name="emulator">Контекст эмулятора, в котором выполняется скрипт.</param>
    /// <param name="state">Состояние скрипта между вычислениями.</param>
    public TagScriptGlobals(
        DateTimeOffset now,
        TagScriptValue tag,
        TagScriptTagAccessor tags,
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state)
        : this(now, tag, tags, emulator, state, null)
    {
    }

    internal TagScriptGlobals(
        DateTimeOffset now,
        TagScriptValue tag,
        TagScriptTagAccessor tags,
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state,
        TagScriptRestContext? rest = null)
    {
        Now = now;
        UniEmu = new UniEmuScriptContext(emulator, state, tag, tags, rest);
    }
}

/// <summary>
/// Контекст UniEmu, доступный скрипту тега.
/// </summary>
[ScriptingApi]
public sealed class UniEmuScriptContext
{
    /// <summary>
    /// Эмулятор, для которого выполняется скрипт.
    /// </summary>
    [ScriptingApi]
    public TagScriptEmulatorContext Emulator { get; }

    /// <summary>
    /// Состояние скрипта, сохраняемое между вычислениями тега.
    /// </summary>
    [ScriptingApi]
    public TagScriptStateContext State { get; }

    /// <summary>
    /// Информация о текущем вычисляемом теге.
    /// </summary>
    [ScriptingApi]
    public TagScriptValue Tag { get; }

    /// <summary>
    /// Доступ к значениям тегов, доступных текущему скрипту.
    /// </summary>
    [ScriptingApi]
    public TagScriptTagAccessor Tags { get; }

    /// <summary>
    /// Настроенные REST-операции, доступные пользовательскому скрипту.
    /// </summary>
    [ScriptingApi]
    public TagScriptRestContext Rest { get; }

    /// <summary>
    /// Создает контекст UniEmu для выполнения скрипта тега.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого выполняется скрипт.</param>
    /// <param name="state">Состояние скрипта между вычислениями.</param>
    /// <param name="tag">Текущий вычисляемый тег.</param>
    /// <param name="tags">Доступ к значениям тегов.</param>
    public UniEmuScriptContext(
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state,
        TagScriptValue tag,
        TagScriptTagAccessor tags)
        : this(emulator, state, tag, tags, null)
    {
    }

    internal UniEmuScriptContext(
        TagScriptEmulatorContext emulator,
        TagScriptStateContext state,
        TagScriptValue tag,
        TagScriptTagAccessor tags,
        TagScriptRestContext? rest = null)
    {
        Emulator = emulator;
        State = state;
        Tag = tag;
        Tags = tags;
        Rest = rest ?? TagScriptRestContext.Disabled;
    }
}
