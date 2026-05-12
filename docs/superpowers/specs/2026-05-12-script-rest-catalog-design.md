# Script REST Catalog Design

## Goal

User scripts need controlled access to an external REST system without exposing `HttpClient`, URLs, headers, serialization, or arbitrary network APIs. Scripts should call a small typed UniEmu API such as:

```csharp
var worker = await UniEmu.Rest.GetWorkerByIdAsync(123);
var active = await UniEmu.Rest.GetActiveWorkerAsync();

await UniEmu.Rest.RegisterWorkerAsync(123);

var result = await UniEmu.Rest.TryRegisterWorkerAsync(123);
if (!result.Success)
    return "register failed";
```

The first implementation uses an appsettings-backed catalog, while the architecture leaves room for a future database-backed catalog without changing script code.

## Chosen Approach

Use a hand-written typed facade over `IHttpClientFactory`.

Scripts see only `UniEmu.Rest` methods and public DTOs from `UniEmu.Scripting.Api`. The backend implementation maps those methods to allowed operations from a REST catalog. This is safer and simpler than exposing a generic REST client or making scripts depend on Refit directly.

Refit can still be introduced later inside the backend for a specific external system, but it should remain an implementation detail. The script-facing API stays stable.

## Script API

`UniEmu.Scripting.Api` gets public scripting types:

```csharp
public sealed class TagScriptRestContext
{
    public Task<Worker?> GetWorkerByIdAsync(int workerId);
    public Task<Worker?> GetActiveWorkerAsync();
    public Task RegisterWorkerAsync(int workerId);
    public Task<RestCallResult> TryRegisterWorkerAsync(int workerId);
}

public sealed class Worker
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Status { get; init; }
    public bool IsActive { get; init; }
}

public sealed class RestCallResult
{
    public bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
}
```

`UniEmuScriptContext` exposes the facade:

```csharp
public TagScriptRestContext Rest { get; }
```

The script API should not expose `HttpClient`, `HttpRequestMessage`, raw URLs, configured headers, or generic request methods.

## Catalog

The first catalog source is configuration:

```json
{
  "UniEmu": {
    "RestCatalog": {
      "DefaultClient": {
        "BaseUrl": "https://external-system.local",
        "TimeoutSeconds": 5,
        "Headers": {
          "Authorization": "Bearer ...",
          "X-Tenant": "demo"
        },
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
  }
}
```

Headers are configured outside scripts. This supports bearer tokens, API keys, tenant headers, and similar schemes without adding authentication concepts to the scripting API.

The backend should use a provider boundary:

```csharp
public interface IRestCatalogProvider
{
    RestCatalogSnapshot GetSnapshot();
}
```

`AppSettingsRestCatalogProvider` reads configuration now. A later `DbRestCatalogProvider` can implement the same interface.

## Backend Components

Add backend-only services under a focused runtime REST area:

- `RestCatalogOptions`, `RestClientOptions`, and `RestOperationOptions` for configuration binding.
- `RestCatalogSnapshot` and `RestOperationDescriptor` as validated immutable runtime descriptors.
- `IRestCatalogProvider` and `AppSettingsRestCatalogProvider`.
- `ITagScriptRestClient` with cancellation-aware methods matching the script facade.
- `TagScriptRestClient` that uses `IHttpClientFactory`, catalog descriptors, configured headers, timeouts, route parameter replacement, JSON deserialization, and response validation.

`TagScriptGlobals` construction injects a `TagScriptRestContext` backed by the current runtime `ITagScriptRestClient` and the current script cancellation token.

## Execution Flow

1. `TagScriptExecutionService.GenerateScriptTagAsync` builds normal tag globals.
2. It also builds `TagScriptRestContext` with the DI-provided backend REST client and current cancellation token.
3. A script calls `await UniEmu.Rest.GetWorkerByIdAsync(123)`.
4. The facade delegates to the backend client.
5. The backend client resolves the named catalog operation, builds the request from the fixed path template, applies configured headers and timeout, sends the request, and deserializes `Worker`.
6. The script receives a typed result and returns the tag value as usual.

Top-level `await` should work because runtime execution already uses Roslyn scripting `RunAsync`. Implementation must verify that diagnostics and validation also accept async scripts that return the expected tag value after awaiting REST operations.

## Error Handling

`GetWorkerByIdAsync` and `GetActiveWorkerAsync`:

- return `null` for HTTP 404;
- throw a script REST exception for non-success status codes other than 404;
- throw for catalog errors, deserialization errors, timeouts, and transport failures.

`RegisterWorkerAsync`:

- completes on 2xx responses;
- throws for non-success responses, catalog errors, timeouts, and transport failures.

`TryRegisterWorkerAsync`:

- returns `RestCallResult.Success = true` on 2xx responses;
- returns `Success = false`, `StatusCode`, and `Error` for expected REST, timeout, transport, and catalog failures;
- should not hide process-level failures such as cancellation.

Runtime logging should record failures with operation name and status code, but must not log configured secret header values.

## Security

`System.Net.*` remains forbidden in user scripts. The only network access from scripts is through `UniEmu.Rest`.

The catalog is an allowlist. Scripts cannot:

- provide arbitrary URLs;
- override configured headers;
- select arbitrary HTTP methods;
- send arbitrary request bodies;
- access raw response headers;
- create `HttpClient` or `HttpRequestMessage`.

The first implementation only supports route parameters required by the approved typed methods. This keeps the request surface intentionally narrow.

Configured headers may contain secrets, so they must stay in backend configuration and out of script DTOs, diagnostics, and log messages.

## IntelliSense And Diagnostics

Because `TagScriptRestContext`, `Worker`, and `RestCallResult` live in `UniEmu.Scripting.Api`, existing CSX metadata references should expose them to Monaco/Roslyn completions and hover.

Tests should confirm:

- completions include `UniEmu.Rest`;
- completions include `GetWorkerByIdAsync`, `GetActiveWorkerAsync`, `RegisterWorkerAsync`, and `TryRegisterWorkerAsync`;
- diagnostics accept top-level `await UniEmu.Rest.RegisterWorkerAsync(123);`;
- diagnostics accept assigning `Worker?` results and reading `Id`, `Name`, `Status`, and `IsActive`.

## Testing

Use test-first implementation for the backend.

Required tests:

- scripting API exposes `UniEmu.Rest` and worker members to IntelliSense;
- async script execution can await a REST facade method and return a tag value;
- security validator still rejects direct `System.Net.Http.HttpClient` usage;
- `GetWorkerByIdAsync` maps a configured GET path, sends configured headers, and deserializes `Worker`;
- `GetWorkerByIdAsync` returns `null` on 404;
- `RegisterWorkerAsync` throws on non-success status;
- `TryRegisterWorkerAsync` returns a failed `RestCallResult` on non-success status without throwing;
- configured secret header values do not appear in exception messages produced by the REST facade.

Existing script runtime, validation, and IntelliSense tests should continue to pass.

## Scope

In scope:

- typed script facade for worker REST operations;
- appsettings-backed allowlist catalog;
- backend provider boundary for future DB catalog;
- configured headers and timeout;
- focused runtime and IntelliSense tests.

Out of scope:

- UI for catalog management;
- storing catalog entries in the database;
- generic REST calls from scripts;
- user-defined request bodies;
- exposing Refit to scripts;
- retry policies and circuit breakers beyond basic timeout handling.
