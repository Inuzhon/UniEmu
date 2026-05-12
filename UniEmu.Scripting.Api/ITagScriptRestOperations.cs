namespace UniEmu.Scripting.Api;

internal interface ITagScriptRestOperations
{
    Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken);

    Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken);

    Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken);

    Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken);
}
