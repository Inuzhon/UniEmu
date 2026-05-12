using Microsoft.Extensions.Configuration;
using UniEmu.Runtime.Scripting.Rest;

namespace UniEmu.Tests.Runtime.Scripting.Rest;

public sealed class AppSettingsRestCatalogProviderTests
{
    [Fact]
    public void GetSnapshot_LoadsDefaultClientAndOperations()
    {
        var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["UniEmu:RestCatalog:DefaultClient:BaseUrl"] = "https://external.local",
            ["UniEmu:RestCatalog:DefaultClient:TimeoutSeconds"] = "7",
            ["UniEmu:RestCatalog:DefaultClient:Headers:Authorization"] = "Bearer secret-token",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Method"] = "GET",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Path"] = "/api/workers/{workerId}",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Response"] = "Worker",
            ["UniEmu:RestCatalog:DefaultClient:Operations:RegisterWorker:Method"] = "POST",
            ["UniEmu:RestCatalog:DefaultClient:Operations:RegisterWorker:Path"] = "/api/workers/{workerId}/register",
        });

        var snapshot = provider.GetSnapshot();
        var getWorker = snapshot.GetDefaultOperation("GetWorkerById");
        var register = snapshot.GetDefaultOperation("RegisterWorker");

        Assert.Equal(new Uri("https://external.local"), getWorker.Client.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(7), getWorker.Client.Timeout);
        Assert.Equal("Bearer secret-token", getWorker.Client.Headers["Authorization"]);
        Assert.Equal(HttpMethod.Get, getWorker.Method);
        Assert.Equal("/api/workers/{workerId}", getWorker.Path);
        Assert.Equal("Worker", getWorker.Response);
        Assert.Equal(HttpMethod.Post, register.Method);
    }

    [Fact]
    public void GetSnapshot_ThrowsSanitizedMessage_WhenBaseUrlIsInvalid()
    {
        var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["UniEmu:RestCatalog:DefaultClient:BaseUrl"] = "not-a-url",
            ["UniEmu:RestCatalog:DefaultClient:Headers:Authorization"] = "Bearer secret-token",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Method"] = "GET",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Path"] = "/api/workers/{workerId}",
        });

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetSnapshot());

        Assert.Contains("DefaultClient", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer secret-token", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapshot_Throws_WhenOperationPathDoesNotStartWithSlash()
    {
        var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["UniEmu:RestCatalog:DefaultClient:BaseUrl"] = "https://external.local",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Method"] = "GET",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Path"] = "api/workers/{workerId}",
        });

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetSnapshot());

        Assert.Contains("GetWorkerById", exception.Message, StringComparison.Ordinal);
        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDefaultOperation_Throws_WhenOperationIsMissing()
    {
        var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["UniEmu:RestCatalog:DefaultClient:BaseUrl"] = "https://external.local",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Method"] = "GET",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Path"] = "/api/workers/{workerId}",
        });

        var snapshot = provider.GetSnapshot();
        var exception = Assert.Throws<InvalidOperationException>(() => snapshot.GetDefaultOperation("RegisterWorker"));

        Assert.Contains("RegisterWorker", exception.Message, StringComparison.Ordinal);
    }

    private static AppSettingsRestCatalogProvider CreateProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new AppSettingsRestCatalogProvider(configuration);
    }
}
