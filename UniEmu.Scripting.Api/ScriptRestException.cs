namespace UniEmu.Scripting.Api;

/// <summary>
/// Error raised when a configured REST operation fails.
/// </summary>
[ScriptingApi]
public sealed class ScriptRestException : Exception
{
    [ScriptingApi]
    public string OperationName { get; }

    [ScriptingApi]
    public int? StatusCode { get; }

    public ScriptRestException(string operationName, int? statusCode, string message)
        : base(message)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }

    public ScriptRestException(string operationName, int? statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }
}
