using Microsoft.Extensions.Configuration;

namespace UniEmu.Runtime.Scripting.Rest;

internal sealed class AppSettingsRestCatalogProvider(IConfiguration configuration) : IRestCatalogProvider
{
    public RestCatalogSnapshot GetSnapshot()
    {
        var options = new RestCatalogOptions();
        configuration.GetSection(RestCatalogOptions.SectionName).Bind(options);

        var clients = new Dictionary<string, RestClientDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (clientName, clientOptions) in options)
        {
            clients[clientName] = CreateClient(clientName, clientOptions);
        }

        return new RestCatalogSnapshot(clients);
    }

    private static RestClientDescriptor CreateClient(string clientName, RestClientOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl))
            throw new InvalidOperationException($"REST catalog client '{clientName}' has an invalid BaseUrl.");

        if (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"REST catalog client '{clientName}' has an unsupported BaseUrl scheme.");

        if (options.TimeoutSeconds <= 0)
            throw new InvalidOperationException($"REST catalog client '{clientName}' must configure a positive timeout.");

        var operations = new Dictionary<string, RestOperationDescriptor>(StringComparer.OrdinalIgnoreCase);
        var client = new RestClientDescriptor(
            clientName,
            baseUrl,
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            new Dictionary<string, string>(options.Headers, StringComparer.OrdinalIgnoreCase),
            operations);

        foreach (var (operationName, operationOptions) in options.Operations)
        {
            operations[operationName] = CreateOperation(client, operationName, operationOptions);
        }

        return client;
    }

    private static RestOperationDescriptor CreateOperation(
        RestClientDescriptor client,
        string operationName,
        RestOperationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Path) ||
            !options.Path.StartsWith("/", StringComparison.Ordinal) ||
            options.Path.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(options.Path, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"REST catalog operation '{operationName}' for client '{client.Name}' must use a path starting with '/'.");
        }

        var method = options.Method?.Trim().ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new InvalidOperationException(
                $"REST catalog operation '{operationName}' for client '{client.Name}' must use GET or POST."),
        };

        return new RestOperationDescriptor(operationName, method, options.Path, options.Response, client);
    }
}
