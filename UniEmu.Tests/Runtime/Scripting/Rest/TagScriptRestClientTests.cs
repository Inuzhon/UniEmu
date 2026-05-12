using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniEmu.Common;
using UniEmu.Runtime.Scripting.Rest;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Runtime.Scripting.Rest;

public sealed class TagScriptRestClientTests
{
    [Fact]
    public async Task GetWorkerByIdAsync_SendsConfiguredRequestAndReadsWorker()
    {
        HttpRequestMessage? sentRequest = null;
        var client = CreateClient(request =>
        {
            sentRequest = request;
            return Json(HttpStatusCode.OK, new Worker { Id = 123, Name = "Worker", Status = "Ready", IsActive = true });
        });

        var worker = await client.GetWorkerByIdAsync(123, CancellationToken.None);

        Assert.NotNull(worker);
        Assert.Equal(123, worker.Id);
        Assert.Equal(HttpMethod.Get, sentRequest?.Method);
        Assert.Equal(new Uri("https://external.local/api/workers/123"), sentRequest?.RequestUri);
        Assert.NotNull(sentRequest);
        Assert.True(sentRequest.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Bearer secret-token", Assert.Single(values));
    }

    [Fact]
    public async Task GetActiveWorkerAsync_MapsConfiguredOperation()
    {
        HttpRequestMessage? sentRequest = null;
        var client = CreateClient(request =>
        {
            sentRequest = request;
            return Json(HttpStatusCode.OK, new Worker { Id = 456, Name = "Active", Status = "Ready", IsActive = true });
        });

        var worker = await client.GetActiveWorkerAsync(CancellationToken.None);

        Assert.NotNull(worker);
        Assert.Equal(456, worker.Id);
        Assert.Equal(new Uri("https://external.local/api/workers/active"), sentRequest?.RequestUri);
    }

    [Fact]
    public async Task RegisterWorkerAsync_PostsConfiguredOperation()
    {
        HttpRequestMessage? sentRequest = null;
        var client = CreateClient(request =>
        {
            sentRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.RegisterWorkerAsync(123, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, sentRequest?.Method);
        Assert.Equal(new Uri("https://external.local/api/workers/123/register"), sentRequest?.RequestUri);
        Assert.Null(sentRequest?.Content);
    }

    [Fact]
    public async Task GetWorkerByIdAsync_ReturnsNullOnNotFound()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var worker = await client.GetWorkerByIdAsync(404, CancellationToken.None);

        Assert.Null(worker);
    }

    [Fact]
    public async Task RegisterWorkerAsync_ThrowsSanitizedExceptionOnNonSuccess()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server mentioned Bearer secret-token"),
        });

        var exception = await Assert.ThrowsAsync<ScriptRestException>(() =>
            client.RegisterWorkerAsync(123, CancellationToken.None));

        Assert.Equal("RegisterWorker", exception.OperationName);
        Assert.Equal(500, exception.StatusCode);
        Assert.Contains("RegisterWorker", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("server mentioned", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryRegisterWorkerAsync_ReturnsFailureOnNonSuccess()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad worker"),
        });

        var result = await client.TryRegisterWorkerAsync(123, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("RegisterWorker", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("bad worker", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryRegisterWorkerAsync_ReturnsFailureOnTransportFailure()
    {
        var client = CreateClient(_ => throw new HttpRequestException("network leaked Bearer secret-token"));

        var result = await client.TryRegisterWorkerAsync(123, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("RegisterWorker", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryRegisterWorkerAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.TryRegisterWorkerAsync(123, cts.Token));
    }

    private static TagScriptRestClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handle)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TagScriptRestClient)))
            .Returns(new HttpClient(new QueueHandler(handle)));

        return new TagScriptRestClient(factory.Object, new FixedCatalogProvider(CreateSnapshot()), NullLogger<TagScriptRestClient>.Instance);
    }

    private static RestCatalogSnapshot CreateSnapshot()
    {
        var operations = new Dictionary<string, RestOperationDescriptor>(StringComparer.OrdinalIgnoreCase);
        var client = new RestClientDescriptor(
            RestCatalogSnapshot.DefaultClientName,
            new Uri("https://external.local"),
            TimeSpan.FromSeconds(5),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer secret-token",
            },
            operations);

        operations["GetWorkerById"] = new RestOperationDescriptor(
            "GetWorkerById",
            HttpMethod.Get,
            "/api/workers/{workerId}",
            "Worker",
            client);
        operations["GetActiveWorker"] = new RestOperationDescriptor(
            "GetActiveWorker",
            HttpMethod.Get,
            "/api/workers/active",
            "Worker",
            client);
        operations["RegisterWorker"] = new RestOperationDescriptor(
            "RegisterWorker",
            HttpMethod.Post,
            "/api/workers/{workerId}/register",
            null,
            client);

        return new RestCatalogSnapshot(new Dictionary<string, RestClientDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            [RestCatalogSnapshot.DefaultClientName] = client,
        });
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T value)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(UniEmuJson.Serialize(value)),
        };
    }

    private sealed class FixedCatalogProvider(RestCatalogSnapshot snapshot) : IRestCatalogProvider
    {
        public RestCatalogSnapshot GetSnapshot() => snapshot;
    }

    private sealed class QueueHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handle(request));
        }
    }
}
