namespace UniEmu.Scripting.Api;

/// <summary>
/// Minimal worker data returned by configured REST operations.
/// </summary>
[ScriptingApi]
public sealed class Worker
{
    [ScriptingApi]
    public int Id { get; init; }

    [ScriptingApi]
    public string? Name { get; init; }

    [ScriptingApi]
    public string? Status { get; init; }

    [ScriptingApi]
    public bool IsActive { get; init; }
}
