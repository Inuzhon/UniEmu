namespace UniEmu.Scripting.Api;

/// <summary>
/// Настроенные REST-операции, доступные пользовательским скриптам.
/// </summary>
[ScriptingApi]
public sealed class TagScriptRestContext
{
    private readonly ITagScriptRestOperations operations;
    private readonly CancellationToken cancellationToken;

    internal TagScriptRestContext(ITagScriptRestOperations operations, CancellationToken cancellationToken = default)
    {
        this.operations = operations;
        this.cancellationToken = cancellationToken;
    }

    internal static TagScriptRestContext Disabled { get; } = CreateDisabled();

    internal static TagScriptRestContext CreateDisabled(CancellationToken cancellationToken = default)
    {
        return new TagScriptRestContext(new DisabledRestOperations(), cancellationToken);
    }

    /// <summary>
    /// Возвращает работника по идентификатору.
    /// </summary>
    /// <param name="workerId">Идентификатор работника.</param>
    /// <returns>Данные работника или <see langword="null"/>, если работник не найден.</returns>
    [ScriptingApi]
    public Task<Worker?> GetWorkerByIdAsync(int workerId)
    {
        return operations.GetWorkerByIdAsync(workerId, cancellationToken);
    }

    /// <summary>
    /// Возвращает текущего активного работника.
    /// </summary>
    /// <returns>Данные активного работника или <see langword="null"/>, если он не найден.</returns>
    [ScriptingApi]
    public Task<Worker?> GetActiveWorkerAsync()
    {
        return operations.GetActiveWorkerAsync(cancellationToken);
    }

    /// <summary>
    /// Регистрирует работника через настроенную REST-операцию.
    /// </summary>
    /// <param name="workerId">Идентификатор работника.</param>
    /// <returns>Задача выполнения операции.</returns>
    [ScriptingApi]
    public Task RegisterWorkerAsync(int workerId)
    {
        return operations.RegisterWorkerAsync(workerId, cancellationToken);
    }

    /// <summary>
    /// Пытается зарегистрировать работника без выброса исключения при ошибке REST-операции.
    /// </summary>
    /// <param name="workerId">Идентификатор работника.</param>
    /// <returns>Результат REST-операции.</returns>
    [ScriptingApi]
    public Task<RestCallResult> TryRegisterWorkerAsync(int workerId)
    {
        return operations.TryRegisterWorkerAsync(workerId, cancellationToken);
    }

    private sealed class DisabledRestOperations : ITagScriptRestOperations
    {
        public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken)
        {
            throw CreateDisabledException("GetWorkerById");
        }

        public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken)
        {
            throw CreateDisabledException("GetActiveWorker");
        }

        public Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
        {
            throw CreateDisabledException("RegisterWorker");
        }

        public Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(RestCallResult.Failed(null, "REST catalog is not configured."));
        }

        private static ScriptRestException CreateDisabledException(string operationName)
        {
            return new ScriptRestException(operationName, null, "REST catalog is not configured.");
        }
    }
}
