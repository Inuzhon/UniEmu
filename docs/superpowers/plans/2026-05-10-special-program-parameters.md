# Special Program Parameters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Calculate `PrgName`, `FrameNum`, `FrameText`, and `Subprogram` from visible CNC programs during runtime publishing.

**Architecture:** Keep base tag generation in `TelemetryValueGenerator`, then apply a runtime enrichment step in `EmulatorPublishJob` after all tag values are known. Resolve visible shared/emulator CNC programs once per publish, let `Subprogram` override `PrgName` for frame calculations, and update received program names into static `PrgName` tags after Dispatcher file receive.

**Tech Stack:** C#/.NET, EF Core SQLite tests, xUnit, existing UniEmu runtime classes.

---

### Task 1: Runtime Publish Tests

**Files:**
- Modify: `UniEmu.Tests/Runtime/EmulatorPublishJobTests.cs`

- [x] **Step 1: Write failing tests**

Add tests proving:
- `BuildValuesAsync` uses subprogram content for `FrameNum` and `FrameText` when both `PrgName` and `Subprogram` exist.
- `BuildValuesAsync` falls back to main program when `Subprogram` is empty.
- `HandleDispatcherAnswerAsync` stores a received program name into a static `PrgName` tag.

- [x] **Step 2: Run tests to verify failure**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter EmulatorPublishJobTests
```

Expected: new tests fail because specialized values are not calculated yet.

### Task 2: Specialized Program Value Enrichment

**Files:**
- Modify: `UniEmu/Runtime/EmulatorPublishJob.cs`

- [x] **Step 1: Implement enrichment after base values**

Inside `BuildValuesAsync`, after generating all values, load visible CNC programs and replace generated `FrameNum`/`FrameText` values for static/scenario tags using the selected program:

```csharp
values = await ApplySpecializedProgramValuesAsync(emulator, values, timestamp, cancellationToken);
```

- [x] **Step 2: Program selection rules**

Resolve `PrgName` and `Subprogram` against visible programs. Match by name case-insensitively, allow `shared` or current emulator scope, prefer `[dispatcher-received]`, then emulator-owned records.

- [x] **Step 3: Frame calculation rules**

Use `Subprogram` when resolved; otherwise `PrgName`. Split content into lines, calculate:

```csharp
var index = (int)(Math.Floor(elapsedSec / Math.Max(1, emulator.IntervalSec)) % lines.Length);
```

Return `FrameNum = index`, `FrameText = lines[index]`; for missing/empty program return `0` and `""`.

- [x] **Step 4: Run tests to verify pass**

Run the same filtered test command and expect all `EmulatorPublishJobTests` to pass.

### Task 3: Received Program Name Persistence

**Files:**
- Modify: `UniEmu/Runtime/EmulatorPublishJob.cs`

- [x] **Step 1: Update static `PrgName` tag after receive**

After `UpsertReceivedProgram`, find enabled/static tag with `SpecialParameter.PrgName`, set its `Preview` to the received program name, and update `TagRuntimeStateStore` with the same string value.

- [x] **Step 2: Verify full runtime test group**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter FullyQualifiedName~UniEmu.Tests.Runtime
```

Expected: runtime tests pass.
