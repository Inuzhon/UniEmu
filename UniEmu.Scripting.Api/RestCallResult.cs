namespace UniEmu.Scripting.Api;

/// <summary>
/// Result of a non-throwing REST operation.
/// </summary>
[ScriptingApi]
public sealed class RestCallResult
{
    [ScriptingApi]
    public bool Success { get; init; }

    [ScriptingApi]
    public int? StatusCode { get; init; }

    [ScriptingApi]
    public string? Error { get; init; }

    public static RestCallResult Ok() => new() { Success = true };

    public static RestCallResult Failed(int? statusCode, string error) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Error = error,
    };
}
