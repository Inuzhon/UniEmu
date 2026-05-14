namespace UniEmu.Scripting.Api;

/// <summary>
/// Минимальные данные работника, возвращаемые настроенными REST-операциями.
/// </summary>
[ScriptingApi]
public sealed class Worker
{
    /// <summary>
    /// Идентификатор работника.
    /// </summary>
    [ScriptingApi]
    public int Id { get; init; }

    /// <summary>
    /// Имя работника.
    /// </summary>
    [ScriptingApi]
    public string? Name { get; init; }

    /// <summary>
    /// Статус работника.
    /// </summary>
    [ScriptingApi]
    public string? Status { get; init; }

    /// <summary>
    /// Показывает, активен ли работник.
    /// </summary>
    [ScriptingApi]
    public bool IsActive { get; init; }
}
