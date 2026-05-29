# .NET 10 Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply practical .NET 10 and C# 14 features across the backend and update editor guidance for future modern syntax.

**Architecture:** Keep behavior changes narrow and covered by tests: JSON gets stricter through the existing `UniEmuJson` helper, OpenAPI mapping gains YAML through the existing startup extension surface, and options validation uses field-backed properties. C# 14 extension blocks replace existing extension-method containers without changing call sites.

**Tech Stack:** .NET 10, C# 14 preview, ASP.NET Core 10 OpenAPI, System.Text.Json, xUnit, EditorConfig analyzer options.

---

### Task 1: Strict JSON Defaults

**Files:**
- Modify: `UniEmu/Common/UniEmuJson.cs`
- Test: `UniEmu.Tests/Common/UniEmuJsonTests.cs`

- [x] **Step 1: Write failing tests**

Add tests that duplicate JSON properties and unmapped JSON members are rejected through both `UniEmuJson.Deserialize<T>` and `UniEmuJson.Apply(...)`.

- [x] **Step 2: Verify red**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter FullyQualifiedName~UniEmu.Tests.Common.UniEmuJsonTests`

Expected: tests fail because current JSON options accept duplicate and unmapped properties.

- [x] **Step 3: Implement strict-compatible web JSON options**

Set `AllowDuplicateProperties = false`, `RespectNullableAnnotations = true`, `RespectRequiredConstructorParameters = true`, and `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` in the shared options and in `Apply`.

- [x] **Step 4: Verify green**

Run the same filtered test command and expect all selected tests to pass.

### Task 2: Options Field-Backed Property

**Files:**
- Modify: `UniEmu/Hosting/UniEmuOptions.cs`
- Test: `UniEmu.Tests/Hosting/UniEmuOptionsTests.cs`

- [x] **Step 1: Write failing test**

Add a test that binds zero or negative `UniEmu:DispatcherBlockCheckIntervalSeconds` and expects the option to clamp to `1`.

- [x] **Step 2: Verify red**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter FullyQualifiedName~UniEmu.Tests.Hosting.UniEmuOptionsTests`

Expected: the new clamp test fails before the property setter changes.

- [x] **Step 3: Implement C# 14 field-backed property**

Change `DispatcherBlockCheckIntervalSeconds` to `get; set => field = Math.Max(1, value);` with initializer `= 5`.

- [x] **Step 4: Verify green**

Run the same filtered test command and expect all selected tests to pass.

### Task 3: OpenAPI YAML Endpoint

**Files:**
- Modify: `UniEmu/Hosting/UniEmuApplicationStartup.cs`
- Modify: `UniEmu/Program.cs`
- Test: `UniEmu.Tests/Hosting/UniEmuApplicationStartupTests.cs`

- [x] **Step 1: Write failing test**

Add a test that builds a minimal `WebApplication`, calls `MapUniEmuOpenApi()`, and asserts JSON and YAML OpenAPI route patterns are registered.

- [x] **Step 2: Verify red**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter FullyQualifiedName~UniEmu.Tests.Hosting.UniEmuApplicationStartupTests`

Expected: test fails to compile or run because `MapUniEmuOpenApi` does not exist yet.

- [x] **Step 3: Implement mapping**

Add `MapUniEmuOpenApi` as a WebApplication extension block member that calls `MapOpenApi()` and `MapOpenApi("/openapi/{documentName}.yaml")`; update `Program.cs` to call it in development.

- [x] **Step 4: Verify green**

Run the same filtered test command and expect all selected tests to pass.

### Task 4: C# 14 Extension Blocks

**Files:**
- Modify: `UniEmu/Mapping/UniEmuMapping.cs`
- Modify: `UniEmu/Hosting/UniEmuServiceCollectionExtensions.cs`
- Modify: `UniEmu/Hosting/UniEmuApplicationStartup.cs`

- [x] **Step 1: Refactor extension methods**

Convert existing `this` extension methods to C# 14 `extension(...)` blocks while preserving all call sites.

- [x] **Step 2: Build**

Run: `dotnet build UniEmu.slnx`

Expected: solution builds, proving extension block syntax and calls are valid.

### Task 5: Runtime Allocation Cleanup

**Files:**
- Modify: `UniEmu/Runtime/TelemetryPacketSender.cs`
- Test: `UniEmu.Tests/Runtime/TelemetryPacketSenderTests.cs`

- [x] **Step 1: Refactor chunk encoding**

Use span-based `Convert.ToBase64String` for program chunks so chunk encoding avoids the intermediate byte-array allocation.

- [x] **Step 2: Verify existing coverage**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --filter FullyQualifiedName~UniEmu.Tests.Runtime.TelemetryPacketSenderTests`

Expected: existing sender tests pass.

### Task 6: EditorConfig Modernization

**Files:**
- Modify: `.editorconfig`

- [x] **Step 1: Add analyzer preferences**

Add suggestions for primary constructors, collection expressions, unbound generic `nameof`, implicitly typed lambdas, simple field-backed accessors, `System.Threading.Lock`, and related modern simplifications.

- [x] **Step 2: Full verification**

Run: `dotnet test UniEmu.Tests/UniEmu.Tests.csproj`

Expected: all backend tests pass.
