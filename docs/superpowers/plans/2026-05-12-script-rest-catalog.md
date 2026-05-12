# Script REST Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe typed REST operations to user CSX scripts through `UniEmu.Rest` without exposing arbitrary HTTP APIs.

**Architecture:** Keep the script-facing contract in `UniEmu.Scripting.Api`, the HTTP/catalog implementation in `UniEmu/Runtime/Scripting/Rest`, and the runtime wiring in `TagScriptExecutionService`. The backend implementation should implement a tiny scripting API port instead of leaking backend services into the API assembly.

**Tech Stack:** .NET 10, Roslyn CSharpScript, ASP.NET Core `IHttpClientFactory`, `IConfiguration`, xUnit, Moq.

---

## File Structure

Create these focused scripting API files:

- `UniEmu.Scripting.Api/Worker.cs`: minimal worker DTO visible to scripts.
- `UniEmu.Scripting.Api/RestCallResult.cs`: non-throwing REST result visible to scripts.
- `UniEmu.Scripting.Api/ScriptRestException.cs`: sanitized exception type for throwing REST methods.
- `UniEmu.Scripting.Api/ITagScriptRestOperations.cs`: backend-implemented port; not marked with `[ScriptingApi]`.
- `UniEmu.Scripting.Api/TagScriptRestContext.cs`: script-facing facade exposed as `UniEmu.Rest`.

Create these backend files:

- `UniEmu/Runtime/Scripting/Rest/RestCatalogOptions.cs`: appsettings binding models.
- `UniEmu/Runtime/Scripting/Rest/RestCatalogSnapshot.cs`: immutable validated catalog descriptors.
- `UniEmu/Runtime/Scripting/Rest/IRestCatalogProvider.cs`: catalog provider boundary for appsettings now and DB later.
- `UniEmu/Runtime/Scripting/Rest/AppSettingsRestCatalogProvider.cs`: configuration-backed provider.
- `UniEmu/Runtime/Scripting/Rest/TagScriptRestClient.cs`: HTTP adapter implementing `ITagScriptRestOperations`.

Modify these existing files:

- `UniEmu.Scripting.Api/TagScriptGlobals.cs`: add `UniEmu.Rest` to the context with a disabled default.
- `UniEmu/Runtime/TagScriptExecutionService.cs`: inject REST operations and pass them into globals.
- `UniEmu/Program.cs`: register the provider, client, and named `HttpClient`.
- `UniEmu/appsettings.json`: add an empty/sample-safe catalog shape with no secrets.

Create or modify tests:

- `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`: IntelliSense and async diagnostics coverage.
- `UniEmu.Tests/Runtime/Scripting/Rest/AppSettingsRestCatalogProviderTests.cs`: catalog validation.
- `UniEmu.Tests/Runtime/Scripting/Rest/TagScriptRestClientTests.cs`: HTTP mapping and sanitized errors.
- `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`: end-to-end script await through `UniEmu.Rest`.

## Task 1: Script API Surface

**Files:**
- Create: `UniEmu.Scripting.Api/Worker.cs`
- Create: `UniEmu.Scripting.Api/RestCallResult.cs`
- Create: `UniEmu.Scripting.Api/ScriptRestException.cs`
- Create: `UniEmu.Scripting.Api/ITagScriptRestOperations.cs`
- Create: `UniEmu.Scripting.Api/TagScriptRestContext.cs`
- Modify: `UniEmu.Scripting.Api/TagScriptGlobals.cs`
- Modify: `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`

- [ ] **Step 1: Write failing IntelliSense and diagnostics tests**

Add these tests to `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs` near the existing `UniEmu` completion tests:

```csharp
[Fact]
public async Task GetCompletionsAsync_ReturnsRestForUniEmuGlobal()
{
    var service = new CsxLanguageService();

    var completions = await service.GetCompletionsAsync(
        "inline/tag-1.csx",
        "UniEmu.",
        7,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals));

    Assert.Contains(completions, item => item.Label == "Rest");
}

[Fact]
public async Task GetCompletionsAsync_ReturnsRestOperationMembers()
{
    var service = new CsxLanguageService();

    var completions = await service.GetCompletionsAsync(
        "inline/tag-1.csx",
        "UniEmu.Rest.",
        12,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals));

    Assert.Contains(completions, item => item.Label == "GetWorkerByIdAsync");
    Assert.Contains(completions, item => item.Label == "GetActiveWorkerAsync");
    Assert.Contains(completions, item => item.Label == "RegisterWorkerAsync");
    Assert.Contains(completions, item => item.Label == "TryRegisterWorkerAsync");
}

[Fact]
public async Task AnalyzeAsync_AcceptsAwaitedRestOperationAndWorkerMembers()
{
    var service = new CsxLanguageService();
    const string content = """
        var worker = await UniEmu.Rest.GetWorkerByIdAsync(123);
        return worker is not null && worker.IsActive
            ? worker.Id
            : -1;
        """;

    var result = await service.AnalyzeAsync(
        "inline/tag-1.csx",
        content,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals),
        typeof(int));

    Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: FAIL because `Rest`, `TagScriptRestContext`, and `Worker` do not exist.

- [ ] **Step 3: Add worker DTO**

Create `UniEmu.Scripting.Api/Worker.cs`:

```csharp
namespace UniEmu.Scripting.Api;

/// <summary>
/// Minimal worker data returned by configured REST operations.
/// </summary>
[ScriptingApi]
public sealed class Worker
{
    [ScriptingApi]
    public int Id { get; init; }

    [ScriptingApi]
    public string? Name { get; init; }

    [ScriptingApi]
    public string? Status { get; init; }

    [ScriptingApi]
    public bool IsActive { get; init; }
}
```

- [ ] **Step 4: Add REST result and exception types**

Create `UniEmu.Scripting.Api/RestCallResult.cs`:

```csharp
namespace UniEmu.Scripting.Api;

/// <summary>
/// Result of a non-throwing REST operation.
/// </summary>
[ScriptingApi]
public sealed class RestCallResult
{
    [ScriptingApi]
    public bool Success { get; init; }

    [ScriptingApi]
    public int? StatusCode { get; init; }

    [ScriptingApi]
    public string? Error { get; init; }

    public static RestCallResult Ok() => new() { Success = true };

    public static RestCallResult Failed(int? statusCode, string error) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Error = error,
    };
}
```

Create `UniEmu.Scripting.Api/ScriptRestException.cs`:

```csharp
namespace UniEmu.Scripting.Api;

/// <summary>
/// Error raised when a configured REST operation fails.
/// </summary>
[ScriptingApi]
public sealed class ScriptRestException : Exception
{
    [ScriptingApi]
    public string OperationName { get; }

    [ScriptingApi]
    public int? StatusCode { get; }

    public ScriptRestException(string operationName, int? statusCode, string message)
        : base(message)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }

    public ScriptRestException(string operationName, int? statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        OperationName = operationName;
        StatusCode = statusCode;
    }
}
```

- [ ] **Step 5: Add backend port and script facade**

Create `UniEmu.Scripting.Api/ITagScriptRestOperations.cs`:

```csharp
namespace UniEmu.Scripting.Api;

public interface ITagScriptRestOperations
{
    Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken);

    Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken);

    Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken);

    Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken);
}
```

Create `UniEmu.Scripting.Api/TagScriptRestContext.cs`:

```csharp
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
```

- [ ] **Step 6: Expose Rest on UniEmu globals**

Modify `UniEmu.Scripting.Api/TagScriptGlobals.cs`:

```csharp
public TagScriptGlobals(
    DateTimeOffset now,
    TagScriptValue tag,
    TagScriptTagAccessor tags,
    TagScriptEmulatorContext emulator,
    TagScriptStateContext state,
    TagScriptRestContext? rest = null)
{
    Now = now;
    UniEmu = new UniEmuScriptContext(emulator, state, tag, tags, rest ?? TagScriptRestContext.Disabled);
}
```

Modify the `UniEmuScriptContext` class in the same file:

```csharp
[ScriptingApi]
public TagScriptRestContext Rest { get; }

public UniEmuScriptContext(
    TagScriptEmulatorContext emulator,
    TagScriptStateContext state,
    TagScriptValue tag,
    TagScriptTagAccessor tags,
    TagScriptRestContext? rest = null)
{
    Emulator = emulator;
    State = state;
    Tag = tag;
    Tags = tags;
    Rest = rest ?? TagScriptRestContext.Disabled;
}
```

- [ ] **Step 7: Run task tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: PASS for the new REST IntelliSense and diagnostics tests.

- [ ] **Step 8: Commit**

Run:

```powershell
git add UniEmu.Scripting.Api UniEmu.Tests\Runtime\Scripting\CsxLanguageServiceTests.cs
git commit -m "Add script REST API surface"
```

## Task 2: Appsettings REST Catalog Provider

**Files:**
- Create: `UniEmu/Runtime/Scripting/Rest/RestCatalogOptions.cs`
- Create: `UniEmu/Runtime/Scripting/Rest/RestCatalogSnapshot.cs`
- Create: `UniEmu/Runtime/Scripting/Rest/IRestCatalogProvider.cs`
- Create: `UniEmu/Runtime/Scripting/Rest/AppSettingsRestCatalogProvider.cs`
- Create: `UniEmu.Tests/Runtime/Scripting/Rest/AppSettingsRestCatalogProviderTests.cs`

- [ ] **Step 1: Write failing provider tests**

Create `UniEmu.Tests/Runtime/Scripting/Rest/AppSettingsRestCatalogProviderTests.cs`:

```csharp
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
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Method"] = "GET",
            ["UniEmu:RestCatalog:DefaultClient:Operations:GetWorkerById:Path"] = "/api/workers/{workerId}",
        });

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetSnapshot());

        Assert.Contains("DefaultClient", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", exception.Message, StringComparison.Ordinal);
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~AppSettingsRestCatalogProviderTests"
```

Expected: FAIL because `UniEmu.Runtime.Scripting.Rest` types do not exist.

- [ ] **Step 3: Add catalog option models**

Create `UniEmu/Runtime/Scripting/Rest/RestCatalogOptions.cs`:

```csharp
namespace UniEmu.Runtime.Scripting.Rest;

public sealed class RestCatalogOptions : Dictionary<string, RestClientOptions>
{
    public const string SectionName = "UniEmu:RestCatalog";
}

public sealed class RestClientOptions
{
    public string? BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = 5;

    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, RestOperationOptions> Operations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RestOperationOptions
{
    public string? Method { get; init; }

    public string? Path { get; init; }

    public string? Response { get; init; }
}
```

- [ ] **Step 4: Add immutable descriptors**

Create `UniEmu/Runtime/Scripting/Rest/RestCatalogSnapshot.cs`:

```csharp
namespace UniEmu.Runtime.Scripting.Rest;

public sealed class RestCatalogSnapshot
{
    private const string DefaultClientName = "DefaultClient";
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

public sealed record RestClientDescriptor(
    string Name,
    Uri BaseUrl,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, RestOperationDescriptor> Operations);

public sealed record RestOperationDescriptor(
    string Name,
    HttpMethod Method,
    string Path,
    string? Response,
    RestClientDescriptor Client);
```

- [ ] **Step 5: Add provider boundary and implementation**

Create `UniEmu/Runtime/Scripting/Rest/IRestCatalogProvider.cs`:

```csharp
namespace UniEmu.Runtime.Scripting.Rest;

public interface IRestCatalogProvider
{
    RestCatalogSnapshot GetSnapshot();
}
```

Create `UniEmu/Runtime/Scripting/Rest/AppSettingsRestCatalogProvider.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace UniEmu.Runtime.Scripting.Rest;

public sealed class AppSettingsRestCatalogProvider(IConfiguration configuration) : IRestCatalogProvider
{
    public RestCatalogSnapshot GetSnapshot()
    {
        var options = new RestCatalogOptions();
        configuration.GetSection(RestCatalogOptions.SectionName).Bind(options);

        var clients = new Dictionary<string, RestClientDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (clientName, clientOptions) in options)
        {
            var client = CreateClient(clientName, clientOptions);
            clients[client.Name] = client;
        }

        return new RestCatalogSnapshot(clients);
    }

    private static RestClientDescriptor CreateClient(string clientName, RestClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new InvalidOperationException("REST catalog contains a client without a name.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl))
            throw new InvalidOperationException($"REST catalog client '{clientName}' has an invalid BaseUrl.");

        if (options.TimeoutSeconds <= 0)
            throw new InvalidOperationException($"REST catalog client '{clientName}' must have a positive timeout.");

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
        if (string.IsNullOrWhiteSpace(operationName))
            throw new InvalidOperationException($"REST catalog client '{client.Name}' contains an operation without a name.");

        var method = options.Method?.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new InvalidOperationException($"REST catalog operation '{operationName}' has an unsupported method."),
        };

        if (string.IsNullOrWhiteSpace(options.Path) || !options.Path.StartsWith('/', StringComparison.Ordinal))
            throw new InvalidOperationException($"REST catalog operation '{operationName}' must have an absolute relative path.");

        return new RestOperationDescriptor(operationName, method, options.Path, options.Response, client);
    }
}
```

- [ ] **Step 6: Run provider tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~AppSettingsRestCatalogProviderTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:

```powershell
git add UniEmu\Runtime\Scripting\Rest UniEmu.Tests\Runtime\Scripting\Rest\AppSettingsRestCatalogProviderTests.cs
git commit -m "Add script REST catalog provider"
```

## Task 3: HTTP REST Client Adapter

**Files:**
- Create: `UniEmu/Runtime/Scripting/Rest/TagScriptRestClient.cs`
- Create: `UniEmu.Tests/Runtime/Scripting/Rest/TagScriptRestClientTests.cs`

- [ ] **Step 1: Write failing HTTP adapter tests**

Create `UniEmu.Tests/Runtime/Scripting/Rest/TagScriptRestClientTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniEmu.Runtime.Scripting.Rest;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Runtime.Scripting.Rest;

public sealed class TagScriptRestClientTests
{
    [Fact]
    public async Task GetWorkerByIdAsync_SendsConfiguredRequestHeadersAndDeserializesWorker()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, new Worker { Id = 123, Name = "Alice", Status = "Ready", IsActive = true });
        });

        var worker = await client.GetWorkerByIdAsync(123, CancellationToken.None);

        Assert.NotNull(worker);
        Assert.Equal(123, worker.Id);
        Assert.Equal("Alice", worker.Name);
        Assert.Equal("Ready", worker.Status);
        Assert.True(worker.IsActive);
        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("https://external.local/api/workers/123", captured?.RequestUri?.ToString());
        Assert.True(captured?.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Bearer secret-token", Assert.Single(values!));
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
        Assert.DoesNotContain("secret-token", exception.Message, StringComparison.Ordinal);
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
            "DefaultClient",
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
            ["DefaultClient"] = client,
        });
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T value)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(value)),
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
            return Task.FromResult(handle(request));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptRestClientTests"
```

Expected: FAIL because `TagScriptRestClient` does not exist.

- [ ] **Step 3: Add HTTP adapter implementation**

Create `UniEmu/Runtime/Scripting/Rest/TagScriptRestClient.cs`:

```csharp
using Microsoft.Extensions.Logging;
using UniEmu.Common;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Rest;

public sealed class TagScriptRestClient(
    IHttpClientFactory httpClientFactory,
    IRestCatalogProvider catalogProvider,
    ILogger<TagScriptRestClient> logger) : ITagScriptRestOperations
{
    public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken)
    {
        return SendWorkerAsync("GetWorkerById", new Dictionary<string, string>
        {
            ["workerId"] = workerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, allowNotFound: true, cancellationToken);
    }

    public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken)
    {
        return SendWorkerAsync("GetActiveWorker", [], allowNotFound: true, cancellationToken);
    }

    public async Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync("RegisterWorker", new Dictionary<string, string>
        {
            ["workerId"] = workerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, cancellationToken);

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
            throw new ScriptRestException(operationName, (int)response.StatusCode, $"REST operation '{operationName}' returned an invalid Worker response.", exception);
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
```

- [ ] **Step 4: Run adapter tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptRestClientTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add UniEmu\Runtime\Scripting\Rest\TagScriptRestClient.cs UniEmu.Tests\Runtime\Scripting\Rest\TagScriptRestClientTests.cs
git commit -m "Add script REST HTTP adapter"
```

## Task 4: Runtime Wiring

**Files:**
- Modify: `UniEmu/Runtime/TagScriptExecutionService.cs`
- Modify: `UniEmu/Program.cs`
- Modify: `UniEmu/appsettings.json`
- Modify: `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`

- [ ] **Step 1: Write failing runtime execution test**

Add this test to `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`:

```csharp
[Fact]
public async Task GenerateScriptTagAsync_CanAwaitRestWorkerOperation()
{
    await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
    await using var db = fixture.CreateDbContext();
    var service = CreateService(
        db,
        new TagRuntimeStateStore(),
        new FakeRestOperations(new Worker { Id = 321, Name = "Active", Status = "Ready", IsActive = true }));
    var (emulator, tag) = await LoadAsync(db, "tg-rest-worker");

    var value = await service.GenerateScriptTagAsync(
        emulator,
        tag,
        DateTimeOffset.Parse("2026-05-11T10:00:00Z"),
        CancellationToken.None);

    Assert.Equal(321, value.Value);
    Assert.Equal(321d, value.NumericValue);
}
```

Update the `CreateService` helper in the same file:

```csharp
private static TagScriptExecutionService CreateService(
    UniEmuDbContext db,
    TagRuntimeStateStore stateStore,
    ITagScriptRestOperations? restOperations = null)
{
    return new TagScriptExecutionService(
        db,
        new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions())),
        stateStore,
        new CompiledTagScriptCache(),
        restOperations: restOperations);
}
```

Add this seeded tag inside `SeedAsync`:

```csharp
CreateScriptTag(
    "tg-rest-worker",
    "Rest worker",
    "rest-worker",
    TagType.Int,
    """
    var worker = await UniEmu.Rest.GetActiveWorkerAsync();
    return worker?.Id ?? -1;
    """),
```

Add this nested fake class near the fixture:

```csharp
private sealed class FakeRestOperations(Worker activeWorker) : ITagScriptRestOperations
{
    public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken)
    {
        return Task.FromResult<Worker?>(activeWorker.Id == workerId ? activeWorker : null);
    }

    public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Worker?>(activeWorker);
    }

    public Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(RestCallResult.Ok());
    }
}
```

Also add `using UniEmu.Scripting.Api;` to the test file if it is not already present.

- [ ] **Step 2: Run runtime test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~GenerateScriptTagAsync_CanAwaitRestWorkerOperation"
```

Expected: FAIL because `TagScriptExecutionService` does not accept or pass REST operations yet.

- [ ] **Step 3: Wire REST operations into script globals**

Modify the primary constructor in `UniEmu/Runtime/TagScriptExecutionService.cs`:

```csharp
public sealed class TagScriptExecutionService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TagRuntimeStateStore stateStore,
    CompiledTagScriptCache scriptCache,
    CsxScriptEnvironment scriptEnvironment,
    CsxScriptDirectiveValidator directiveValidator,
    CsxScriptSecurityValidator securityValidator,
    ITagScriptRestOperations? restOperations = null)
```

Update the convenience constructor so unit tests can inject a fake REST port without constructing every scripting dependency:

```csharp
public TagScriptExecutionService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TagRuntimeStateStore stateStore,
    CompiledTagScriptCache scriptCache,
    ITagScriptRestOperations? restOperations = null)
    : this(
        db,
        dataCache,
        stateStore,
        scriptCache,
        new CsxScriptEnvironment(),
        new CsxScriptDirectiveValidator(),
        new CsxScriptSecurityValidator(),
        restOperations)
{
}
```

Update `BuildGlobals` signature and call:

```csharp
var globals = BuildGlobals(emulator, tag, timestamp, stateValues, cancellationToken);
```

```csharp
private TagScriptGlobals BuildGlobals(
    EmulatorEntity emulator,
    EmulatorTagEntity tag,
    DateTimeOffset timestamp,
    Dictionary<string, object?> stateValues,
    CancellationToken cancellationToken)
```

Pass the REST context at the end of `new TagScriptGlobals(...)`:

```csharp
new TagScriptStateContext(
    emulator.Status == nameof(EmulatorStatus.Running),
    previous?.Value,
    previous?.NumericValue,
    previous?.Timestamp,
    ToScriptStateValues(stateValues)
),
restOperations is null
    ? TagScriptRestContext.CreateDisabled(cancellationToken)
    : new TagScriptRestContext(restOperations, cancellationToken)
```

- [ ] **Step 4: Register backend services**

Modify `UniEmu/Program.cs` usings:

```csharp
using UniEmu.Runtime.Scripting.Rest;
using UniEmu.Scripting.Api;
```

Register a named client near the existing `TelemetryPacketSender` client:

```csharp
builder.Services.AddHttpClient(nameof(TagScriptRestClient));
```

Register Autofac services in `RegisterUniEmuServices`:

```csharp
container.RegisterType<AppSettingsRestCatalogProvider>().As<IRestCatalogProvider>().SingleInstance();
container.RegisterType<TagScriptRestClient>().As<ITagScriptRestOperations>().InstancePerLifetimeScope();
```

- [ ] **Step 5: Add safe appsettings catalog shape**

Modify `UniEmu/appsettings.json` under `UniEmu`:

```json
"RestCatalog": {
  "DefaultClient": {
    "BaseUrl": "https://external-system.local",
    "TimeoutSeconds": 5,
    "Headers": {},
    "Operations": {
      "GetWorkerById": {
        "Method": "GET",
        "Path": "/api/workers/{workerId}",
        "Response": "Worker"
      },
      "GetActiveWorker": {
        "Method": "GET",
        "Path": "/api/workers/active",
        "Response": "Worker"
      },
      "RegisterWorker": {
        "Method": "POST",
        "Path": "/api/workers/{workerId}/register"
      }
    }
  }
}
```

Keep `Headers` empty in committed settings so no secret-like value enters source control.

- [ ] **Step 6: Run runtime test**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~GenerateScriptTagAsync_CanAwaitRestWorkerOperation"
```

Expected: PASS.

- [ ] **Step 7: Run focused script runtime/security tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptExecutionServiceTests|FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: PASS, including the existing direct `System.Net.Http.HttpClient` rejection.

- [ ] **Step 8: Commit**

Run:

```powershell
git add UniEmu\Runtime\TagScriptExecutionService.cs UniEmu\Program.cs UniEmu\appsettings.json UniEmu.Tests\Runtime\TagScriptExecutionServiceTests.cs UniEmu.Scripting.Api\TagScriptRestContext.cs
git commit -m "Wire script REST operations into runtime"
```

## Task 5: Final Verification

**Files:**
- Verify all files changed by Tasks 1-4.

- [ ] **Step 1: Run full backend test suite**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True
```

Expected: PASS. `NETSDK1057` preview SDK warnings are acceptable.

- [ ] **Step 2: Build backend test project**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet build UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True /v:minimal
```

Expected: build succeeds.

- [ ] **Step 3: Review git diff for architecture boundaries**

Run:

```powershell
git diff --stat HEAD~4..HEAD
git diff HEAD~4..HEAD -- UniEmu.Scripting.Api UniEmu\Runtime\Scripting\Rest UniEmu\Runtime\TagScriptExecutionService.cs UniEmu\Program.cs
```

Expected:

- `UniEmu.Scripting.Api` contains only DTOs, exception, context, and the small operations port.
- `UniEmu/Runtime/Scripting/Rest` contains all catalog and HTTP details.
- `TagScriptExecutionService` only wires `TagScriptRestContext`; it does not build HTTP requests.
- No configured header secret values appear in committed files or exception messages.

- [ ] **Step 4: Commit any verification fixes**

If verification required code fixes, commit only those fixes:

```powershell
git add UniEmu.Scripting.Api UniEmu\Runtime\Scripting\Rest UniEmu\Runtime\TagScriptExecutionService.cs UniEmu\Program.cs UniEmu\appsettings.json UniEmu.Tests\Runtime\Scripting UniEmu.Tests\Runtime\TagScriptExecutionServiceTests.cs
git commit -m "Stabilize script REST catalog"
```

If no fixes were required, do not create an empty commit.
