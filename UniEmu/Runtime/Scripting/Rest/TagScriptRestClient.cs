using System.Globalization;
using Microsoft.Extensions.Logging;
using UniEmu.Common;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Rest;

internal sealed class TagScriptRestClient(
    IHttpClientFactory httpClientFactory,
    IRestCatalogProvider catalogProvider,
    ILogger<TagScriptRestClient> logger) : ITagScriptRestOperations
{
    public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken)
    {
        return SendWorkerAsync(
            "GetWorkerById",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workerId"] = workerId.ToString(CultureInfo.InvariantCulture),
            },
            allowNotFound: true,
            cancellationToken);
    }

    public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken)
    {
        return SendWorkerAsync("GetActiveWorker", new Dictionary<string, string>(StringComparer.Ordinal), true, cancellationToken);
    }

    public async Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            "RegisterWorker",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workerId"] = workerId.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw CreateStatusException("RegisterWorker", response);
    }

    public async Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            await RegisterWorkerAsync(workerId, cancellationToken);
            return RestCallResult.Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ScriptRestException exception)
        {
            return RestCallResult.Failed(exception.StatusCode, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(exception, "REST operation RegisterWorker failed.");
            return RestCallResult.Failed(null, "REST operation 'RegisterWorker' failed.");
        }
    }

    private async Task<Worker?> SendWorkerAsync(
        string operationName,
        IReadOnlyDictionary<string, string> routeValues,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(operationName, routeValues, cancellationToken);
        if (allowNotFound && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw CreateStatusException(operationName, response);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return UniEmuJson.Deserialize<Worker>(content);
        }
        catch (Exception exception)
        {
            throw new ScriptRestException(
                operationName,
                (int)response.StatusCode,
                $"REST operation '{operationName}' returned an invalid Worker response.",
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        string operationName,
        IReadOnlyDictionary<string, string> routeValues,
        CancellationToken cancellationToken)
    {
        var operation = catalogProvider.GetSnapshot().GetDefaultOperation(operationName);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(operation.Client.Timeout);

        var client = httpClientFactory.CreateClient(nameof(TagScriptRestClient));
        using var request = new HttpRequestMessage(operation.Method, BuildUri(operation, routeValues));
        foreach (var (name, value) in operation.Client.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        try
        {
            return await client.SendAsync(request, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new ScriptRestException(operationName, null, $"REST operation '{operationName}' failed.", exception);
        }
    }

    private static Uri BuildUri(RestOperationDescriptor operation, IReadOnlyDictionary<string, string> routeValues)
    {
        var path = operation.Path;
        foreach (var (key, value) in routeValues)
        {
            path = path.Replace("{" + key + "}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        }

        if (path.Contains('{', StringComparison.Ordinal) || path.Contains('}', StringComparison.Ordinal))
            throw new InvalidOperationException($"REST operation '{operation.Name}' has unresolved route parameters.");

        return new Uri(operation.Client.BaseUrl, path);
    }

    private static ScriptRestException CreateStatusException(string operationName, HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        return new ScriptRestException(operationName, statusCode, $"REST operation '{operationName}' failed with HTTP {statusCode}.");
    }
}
