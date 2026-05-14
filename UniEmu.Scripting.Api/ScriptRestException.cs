namespace UniEmu.Scripting.Api;

/// <summary>
/// Ошибка, возникающая при неуспешном выполнении настроенной REST-операции.
/// </summary>
[ScriptingApi]
public sealed class ScriptRestException : Exception
{
    /// <summary>
    /// Имя REST-операции, во время которой произошла ошибка.
    /// </summary>
    [ScriptingApi]
    public string OperationName { get; }

    /// <summary>
    /// HTTP-статус ответа, если он был получен.
    /// </summary>
    [ScriptingApi]
    public int? StatusCode { get; }

    /// <summary>
    /// Создает исключение REST-операции.
    /// </summary>
    /// <param name="operationName">Имя REST-операции.</param>
    /// <param name="statusCode">HTTP-статус ответа, если он был получен.</param>
    /// <param name="message">Описание ошибки.</param>
    public ScriptRestException(string operationName, int? statusCode, string message)
        : base(message)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Создает исключение REST-операции с исходным исключением.
    /// </summary>
    /// <param name="operationName">Имя REST-операции.</param>
    /// <param name="statusCode">HTTP-статус ответа, если он был получен.</param>
    /// <param name="message">Описание ошибки.</param>
    /// <param name="innerException">Исключение, вызвавшее ошибку.</param>
    public ScriptRestException(string operationName, int? statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }
}
