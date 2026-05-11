namespace UniEmu.Scripting.Api;

/// <summary>
/// Глобальные объекты, доступные скрипту при вычислении тега.
/// </summary>
public sealed class TagScriptGlobals
{
    /// <summary>
    /// Текущее время вычисления тега в часовом поясе приложения.
    /// </summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// Контекст UniEmu с данными эмулятора, текущего тега, других тегов и состояния скрипта.
    /// </summary>
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
    {
        Now = now;
        UniEmu = new UniEmuScriptContext(emulator, state, tag, tags);
    }
}

/// <summary>
/// Контекст UniEmu, доступный скрипту тега.
/// </summary>
public sealed class UniEmuScriptContext
{
    /// <summary>
    /// Эмулятор, для которого выполняется скрипт.
    /// </summary>
    public TagScriptEmulatorContext Emulator { get; }

    /// <summary>
    /// Состояние скрипта, сохраняемое между вычислениями тега.
    /// </summary>
    public TagScriptStateContext State { get; }

    /// <summary>
    /// Информация о текущем вычисляемом теге.
    /// </summary>
    public TagScriptValue Tag { get; }

    /// <summary>
    /// Доступ к значениям тегов, доступных текущему скрипту.
    /// </summary>
    public TagScriptTagAccessor Tags { get; }

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
    {
        Emulator = emulator;
        State = state;
        Tag = tag;
        Tags = tags;
    }
}
