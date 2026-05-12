namespace UniEmu.Runtime.Scripting.Rest;

internal sealed class RestCatalogSnapshot
{
    internal const string DefaultClientName = "DefaultClient";
    private readonly IReadOnlyDictionary<string, RestClientDescriptor> clients;

    public RestCatalogSnapshot(IReadOnlyDictionary<string, RestClientDescriptor> clients)
    {
        this.clients = clients;
    }

    public RestOperationDescriptor GetDefaultOperation(string operationName)
    {
        if (!clients.TryGetValue(DefaultClientName, out var client))
            throw new InvalidOperationException("REST catalog client 'DefaultClient' is not configured.");

        if (!client.Operations.TryGetValue(operationName, out var operation))
            throw new InvalidOperationException($"REST catalog operation '{operationName}' is not configured.");

        return operation;
    }
}

internal sealed record RestClientDescriptor(
    string Name,
    Uri BaseUrl,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, RestOperationDescriptor> Operations);

internal sealed record RestOperationDescriptor(
    string Name,
    HttpMethod Method,
    string Path,
    string? Response,
    RestClientDescriptor Client);
