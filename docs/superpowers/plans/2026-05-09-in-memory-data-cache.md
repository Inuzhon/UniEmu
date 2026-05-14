# In-Memory Data Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a minimal in-process caching layer for frequently read configuration data so runtime and API services do not repeatedly query SQLite for stable data.

**Architecture:** Add `CachedUniEmuDataService` backed by `IMemoryCache`. Read methods return no-tracking entity snapshots for emulators with tags, visible scripts, and visible CNC programs; write services invalidate exact cache regions after successful writes.

**Tech Stack:** ASP.NET Core, EF Core SQLite, `Microsoft.Extensions.Caching.Memory`, xUnit.

---

### Task 1: Cache Service Behavior

**Files:**
- Create: `UniEmu/Data/CachedUniEmuDataService.cs`
- Test: `UniEmu.Tests/Data/CachedUniEmuDataServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests that create an in-memory SQLite database, seed one emulator with tags/scripts/programs, call the cache service twice, and assert the second read performs no additional SQL command. Add an invalidation test that updates the database, clears the emulator cache, and asserts the next read returns fresh data.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter CachedUniEmuDataServiceTests`

Expected: FAIL because `CachedUniEmuDataService` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `CachedUniEmuDataService` with `GetRunningEmulatorWithTagsAsync`, `GetVisibleScriptsAsync`, `GetVisibleCncProgramsAsync`, `InvalidateEmulator`, `InvalidateScripts`, and `InvalidateCncPrograms`. Use absolute expiration of one minute and cache key strings scoped by emulator id.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter CachedUniEmuDataServiceTests`

Expected: PASS.

### Task 2: Runtime Integration

**Files:**
- Modify: `UniEmu/Runtime/EmulatorPublishJob.cs`
- Modify: `UniEmu/Runtime/TagScriptExecutionService.cs`
- Modify: `UniEmu/Program.cs`

- [ ] **Step 1: Inject cache service**

Register `IMemoryCache` and `CachedUniEmuDataService`. Inject it into runtime classes that repeatedly load emulator tags, scripts, or CNC programs.

- [ ] **Step 2: Replace hot reads**

Use cached emulator snapshots in `EmulatorPublishJob`, cached visible scripts in `TagScriptExecutionService`, and cached CNC program snapshots when resolving Dispatcher-requested programs.

- [ ] **Step 3: Run runtime tests**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter Runtime`

Expected: PASS.

### Task 3: Write-Side Invalidation

**Files:**
- Modify: `UniEmu/Features/Emulators/EmulatorService.cs`
- Modify: `UniEmu/Features/Tags/TagService.cs`
- Modify: `UniEmu/Features/Scripts/ScriptService.cs`
- Modify: `UniEmu/Features/CncPrograms/CncProgramService.cs`

- [ ] **Step 1: Inject cache service into write services**

Add `CachedUniEmuDataService` to constructors for services that mutate cached tables.

- [ ] **Step 2: Invalidate after successful writes**

After successful `SaveChangesAsync` or `ExecuteDeleteAsync`, invalidate affected emulator, script, or CNC program cache entries. For tag writes, invalidate the parent emulator because runtime reads tags through emulator snapshots.

- [ ] **Step 3: Run full backend tests**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj`

Expected: PASS.
