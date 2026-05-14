namespace UniEmu.Scripting.Api;

/// <summary>
/// Результат REST-операции, которая не выбрасывает исключение при ошибке.
/// </summary>
[ScriptingApi]
public sealed class RestCallResult
{
    /// <summary>
    /// Показывает, завершилась ли REST-операция успешно.
    /// </summary>
    [ScriptingApi]
    public bool Success { get; init; }

    /// <summary>
    /// HTTP-статус ответа, если он был получен.
    /// </summary>
    [ScriptingApi]
    public int? StatusCode { get; init; }

    /// <summary>
    /// Текст ошибки при неуспешной операции.
    /// </summary>
    [ScriptingApi]
    public string? Error { get; init; }

    /// <summary>
    /// Создает успешный результат REST-операции.
    /// </summary>
    /// <returns>Успешный результат.</returns>
    public static RestCallResult Ok() => new() { Success = true };

    /// <summary>
    /// Создает неуспешный результат REST-операции.
    /// </summary>
    /// <param name="statusCode">HTTP-статус ответа, если он был получен.</param>
    /// <param name="error">Описание ошибки.</param>
    /// <returns>Неуспешный результат.</returns>
    public static RestCallResult Failed(int? statusCode, string error) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Error = error,
    };
}
