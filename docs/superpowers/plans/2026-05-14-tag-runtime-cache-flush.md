# Tag Runtime Cache Flush Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move tag preview persistence out of the per-tick hot path while keeping live tag calculations fresh through runtime state.

**Architecture:** `TagValueJob` reads immutable emulator/tag configuration through `CachedUniEmuDataService`, updates `TagRuntimeStateStore`, and marks previews dirty. A new singleton `TagPreviewFlushService` coalesces dirty previews and persists them in batches through a scoped `UniEmuDbContext`; shutdown persistence remains the final checkpoint.

**Tech Stack:** .NET 10 preview, EF Core, Autofac, Quartz, xUnit, Moq, SQLite in-memory tests, `IMemoryCache`.

---

## File Structure

- Modify `UniEmu/Runtime/TagValueJob.cs`: switch from per-tick tracked DB reads/writes to cached snapshot reads and dirty preview marking.
- Create `UniEmu/Runtime/TagPreviewFlushService.cs`: in-memory dirty preview queue plus batch flush to `EmulatorTags.Preview`.
- Modify `UniEmu/Runtime/TagScriptExecutionService.cs`: mark static tag side-effect previews dirty when scripts update static tags.
- Modify `UniEmu/Runtime/EmulatorScheduleService.cs`: flush pending previews when stopping or unscheduling an emulator.
- Modify `UniEmu/Runtime/TagRuntimeStatePersistenceService.cs`: optionally use flush service or preserve existing full snapshot persistence while coexisting with dirty previews.
- Modify `UniEmu/Hosting/UniEmuBackendServiceRegistration.cs`: register `TagPreviewFlushService` as singleton and inject it into runtime services.
- Modify `UniEmu.Tests/Runtime/TagValueJobTests.cs`: cover cached config reads and deferred writes.
- Create `UniEmu.Tests/Runtime/TagPreviewFlushServiceTests.cs`: cover coalescing, flush, delete races, and retry behavior.
- Modify `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`: cover static tag side-effect dirty marking if an existing test can be extended cleanly.

## Task 1: Add Dirty Preview Flush Service Tests

**Files:**
- Create: `UniEmu.Tests/Runtime/TagPreviewFlushServiceTests.cs`
- Use existing helpers from: `UniEmu.Tests/Common/RecordingDbCommandInterceptor.cs`

- [ ] **Step 1: Write failing tests for dirty preview flush**

Create `UniEmu.Tests/Runtime/TagPreviewFlushServiceTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TagPreviewFlushServiceTests
{
    [Fact]
    public async Task FlushAsync_WritesDirtyPreviewToDatabase()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-1", "42");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var preview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("42", preview);
    }

    [Fact]
    public async Task FlushAsync_CoalescesMultipleDirtyValues()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-1", "1");
        service.MarkDirty("em-1", "tg-1", "2");
        service.MarkDirty("em-1", "tg-1", "3");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var preview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("3", preview);
    }

    [Fact]
    public async Task FlushAsync_SkipsDeletedTags()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-missing", "99");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var existingPreview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("0", existingPreview);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private Fixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<UniEmuDbContext>().UseSqlite(connection).Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.EmulatorTags.Add(new EmulatorTagEntity
            {
                Id = "tg-1",
                EmulatorId = "em-1",
                Name = "Temperature",
                Key = "temperature",
                Type = "double",
                Source = "generator",
                Preview = "0",
                TriggerJson = "{}",
            });
            await db.SaveChangesAsync();

            return new Fixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagPreviewFlushServiceTests"
```

Expected: build fails because `TagPreviewFlushService` does not exist.

- [ ] **Step 3: Implement minimal `TagPreviewFlushService`**

Create `UniEmu/Runtime/TagPreviewFlushService.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using UniEmu.Data;

namespace UniEmu.Runtime;

public sealed class TagPreviewFlushService(
    Func<UniEmuDbContext> dbContextFactory,
    ILogger<TagPreviewFlushService> logger)
{
    private readonly ConcurrentDictionary<TagPreviewKey, string> dirtyPreviews = new();

    public void MarkDirty(string emulatorId, string tagId, string preview)
    {
        dirtyPreviews[new TagPreviewKey(emulatorId, tagId)] = preview;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var batch = dirtyPreviews.ToArray();
        if (batch.Length == 0)
        {
            return;
        }

        foreach (var item in batch)
        {
            dirtyPreviews.TryRemove(item.Key, out _);
        }

        try
        {
            await using var db = dbContextFactory();
            foreach (var item in batch)
            {
                await db.EmulatorTags
                    .Where(t => t.EmulatorId == item.Key.EmulatorId && t.Id == item.Key.TagId)
                    .ExecuteUpdateAsync(
                        update => update.SetProperty(t => t.Preview, item.Value),
                        cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            foreach (var item in batch)
            {
                dirtyPreviews[item.Key] = item.Value;
            }

            logger.LogWarning(ex, "Failed to flush tag previews");
        }
    }

    private sealed record TagPreviewKey(string EmulatorId, string TagId);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command.

Expected: `TagPreviewFlushServiceTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add UniEmu/Runtime/TagPreviewFlushService.cs UniEmu.Tests/Runtime/TagPreviewFlushServiceTests.cs
git commit -m "feat: add deferred tag preview flush service"
```

## Task 2: Switch TagValueJob To Cached Config And Deferred Writes

**Files:**
- Modify: `UniEmu/Runtime/TagValueJob.cs`
- Modify: `UniEmu.Tests/Runtime/TagValueJobTests.cs`

- [ ] **Step 1: Write failing tests for no immediate preview write and repeated cached read**

Extend `TagValueJobTests` with a fixture command counter or use a new fixture based on `RecordingDbCommandInterceptor`. Add:

```csharp
[Fact]
public async Task Execute_DoesNotPersistPreviewBeforeFlush()
{
    await using var fixture = await TagValueJobDbFixture.CreateAsync();
    await using var db = fixture.CreateDbContext();
    var stateStore = new TagRuntimeStateStore();
    var cache = new MemoryCache(new MemoryCacheOptions());
    var dataCache = new CachedUniEmuDataService(db, cache);
    var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
    var job = CreateJob(db, dataCache, stateStore, flushService);

    await job.Execute(CreateContext("tg-cron"));

    db.ChangeTracker.Clear();
    var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-cron");
    Assert.Equal("(computed)", tag.Preview);
    Assert.True(stateStore.TryGet("em-1", "tg-cron", out var value));
    Assert.Equal(17d, value.Value);
}
```

Add a helper:

```csharp
private static TagValueJob CreateJob(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TagRuntimeStateStore stateStore,
    TagPreviewFlushService flushService)
{
    return new TagValueJob(
        dataCache,
        new TelemetryValueGenerator(),
        new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
        stateStore,
        flushService,
        new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
        NullLogger<TagValueJob>.Instance);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagValueJobTests.Execute_DoesNotPersistPreviewBeforeFlush"
```

Expected: build fails because `TagValueJob` does not accept `CachedUniEmuDataService` and `TagPreviewFlushService`, or assertion fails because preview is written immediately.

- [ ] **Step 3: Modify `TagValueJob` minimally**

Change constructor dependencies from `UniEmuDbContext db` to `CachedUniEmuDataService dataCache` and add `TagPreviewFlushService previewFlushService`. Replace the tracked query with:

```csharp
var emulator = await dataCache.GetEmulatorWithTagsAsync(emulatorId, cancellationToken);
var tag = emulator?.Tags.FirstOrDefault(t => t.Id == tagId);

if (emulator is null || tag is null || emulator.Status != nameof(EmulatorStatus.Running))
{
    stateStore.Remove(emulatorId, tagId);
    return;
}
```

After value generation replace immediate `db.SaveChangesAsync` with:

```csharp
var preview = TelemetryValueGenerator.ToPreview(value.Value);
tag.Preview = preview;
stateStore.Set(emulatorId, tagId, tag.Name, value.Value, value.NumericValue, now);
previewFlushService.MarkDirty(emulatorId, tagId, preview);
```

For error events, keep a scoped write path by adding a method to `TagPreviewFlushService` later only if needed, or keep `UniEmuDbContext` in `TagValueJob` solely for `SystemEvents`. Prefer keeping `UniEmuDbContext db` for error event writes if it avoids broad changes:

```csharp
UniEmuDbContext db,
CachedUniEmuDataService dataCache,
...
```

but do not use `db` for successful tag reads or preview writes.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagValueJobTests"
```

Expected: tag job tests pass after updating test constructors.

- [ ] **Step 5: Commit**

```powershell
git add UniEmu/Runtime/TagValueJob.cs UniEmu.Tests/Runtime/TagValueJobTests.cs
git commit -m "perf: defer tag value preview writes"
```

## Task 3: Register Flush Service And Integrate Lifecycle Flush

**Files:**
- Modify: `UniEmu/Hosting/UniEmuBackendServiceRegistration.cs`
- Modify: `UniEmu/Runtime/EmulatorScheduleService.cs`
- Modify: `UniEmu/Runtime/TagRuntimeStatePersistenceService.cs`
- Modify: relevant constructor tests under `UniEmu.Tests/Runtime`

- [ ] **Step 1: Write failing test for unschedule flush**

In `EmulatorScheduleServiceTests`, add or extend a test that marks a dirty preview, calls `UnscheduleEmulatorAsync`, and asserts DB preview persisted:

```csharp
flushService.MarkDirty("em-1", "tg-1", "123");

await service.UnscheduleEmulatorAsync("em-1", CancellationToken.None);

var preview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
Assert.Equal("123", preview);
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~EmulatorScheduleServiceTests"
```

Expected: assertion fails because unschedule does not flush dirty previews yet.

- [ ] **Step 3: Register and inject `TagPreviewFlushService`**

In `UniEmuBackendServiceRegistration.RegisterServices` add:

```csharp
container.Register(context =>
        new TagPreviewFlushService(
            () => context.Resolve<UniEmuDbContext>(),
            context.Resolve<ILogger<TagPreviewFlushService>>()))
    .AsSelf()
    .SingleInstance();
```

Inject `TagPreviewFlushService` into `EmulatorScheduleService` and call:

```csharp
await previewFlushService.FlushAsync(cancellationToken);
```

at the end of `UnscheduleEmulatorAsync` after clearing runtime state. If the service stops an emulator through another path, add the flush there too.

- [ ] **Step 4: Run focused runtime tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~EmulatorScheduleServiceTests|FullyQualifiedName~TagValueJobTests|FullyQualifiedName~TagPreviewFlushServiceTests"
```

Expected: focused runtime tests pass.

- [ ] **Step 5: Commit**

```powershell
git add UniEmu/Hosting/UniEmuBackendServiceRegistration.cs UniEmu/Runtime/EmulatorScheduleService.cs UniEmu/Runtime/TagRuntimeStatePersistenceService.cs UniEmu.Tests/Runtime
git commit -m "feat: flush tag previews during runtime lifecycle"
```

## Task 4: Preserve Script Static Tag Side Effects

**Files:**
- Modify: `UniEmu/Runtime/TagScriptExecutionService.cs`
- Modify: `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`

- [ ] **Step 1: Write failing test for static tag side-effect persistence**

Add a test where a script sets a static tag through `UniEmu.Tags`, then flushes dirty previews and asserts the static tag preview is persisted. The assertion should read:

```csharp
Assert.Equal("77", staticTag.Preview);
```

after:

```csharp
await previewFlushService.FlushAsync(CancellationToken.None);
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptExecutionServiceTests"
```

Expected: static tag runtime state updates, but deferred flush does not know the static tag is dirty.

- [ ] **Step 3: Inject and use `TagPreviewFlushService` in script execution**

Add `TagPreviewFlushService previewFlushService` to `TagScriptExecutionService` constructors used by DI and tests. In `SetStaticTag`, after `tag.Preview = ...`, add:

```csharp
previewFlushService.MarkDirty(emulator.Id, tag.Id, tag.Preview);
```

- [ ] **Step 4: Run focused script tests**

Run the same filtered command.

Expected: script execution tests pass.

- [ ] **Step 5: Commit**

```powershell
git add UniEmu/Runtime/TagScriptExecutionService.cs UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs UniEmu/Hosting/UniEmuBackendServiceRegistration.cs UniEmu.Tests/Runtime/TagValueJobTests.cs
git commit -m "fix: persist script static tag side effects via preview flush"
```

## Task 5: Final Verification

**Files:**
- Review all touched files.

- [ ] **Step 1: Run backend runtime tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~Runtime|FullyQualifiedName~CachedUniEmuDataServiceTests|FullyQualifiedName~UniEmuBackendServiceRegistrationTests"
```

Expected: all selected tests pass.

- [ ] **Step 2: Run full backend test project**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True
```

Expected: all tests pass.

- [ ] **Step 3: Inspect git diff**

Run:

```powershell
git diff --stat
git diff -- UniEmu/Runtime UniEmu/Hosting UniEmu.Tests/Runtime
```

Expected: changes are limited to runtime cache/flush behavior and related tests.

- [ ] **Step 4: Commit final adjustments if needed**

If any verification-only fixes were made:

```powershell
git add UniEmu UniEmu.Tests docs/superpowers/plans/2026-05-14-tag-runtime-cache-flush.md
git commit -m "test: verify tag runtime cache flush"
```
