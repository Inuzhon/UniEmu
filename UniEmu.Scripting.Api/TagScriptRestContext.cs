namespace UniEmu.Scripting.Api;

/// <summary>
/// Configured REST operations available to user scripts.
/// </summary>
[ScriptingApi]
public sealed class TagScriptRestContext
{
    private readonly ITagScriptRestOperations operations;
    private readonly CancellationToken cancellationToken;

    public TagScriptRestContext(ITagScriptRestOperations operations, CancellationToken cancellationToken = default)
    {
        this.operations = operations;
        this.cancellationToken = cancellationToken;
    }

    public static TagScriptRestContext Disabled { get; } = CreateDisabled();

    public static TagScriptRestContext CreateDisabled(CancellationToken cancellationToken = default)
    {
        return new TagScriptRestContext(new DisabledRestOperations(), cancellationToken);
    }

    [ScriptingApi]
    public Task<Worker?> GetWorkerByIdAsync(int workerId)
    {
        return operations.GetWorkerByIdAsync(workerId, cancellationToken);
    }

    [ScriptingApi]
    public Task<Worker?> GetActiveWorkerAsync()
    {
        return operations.GetActiveWorkerAsync(cancellationToken);
    }

    [ScriptingApi]
    public Task RegisterWorkerAsync(int workerId)
    {
        return operations.RegisterWorkerAsync(workerId, cancellationToken);
    }

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
