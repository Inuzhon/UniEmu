# Dispatcher XML Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add backend generation and download of Universal dispatcher XML templates for emulator tags.

**Architecture:** Introduce a focused `DispatcherTemplateService` that reads one emulator with tags and builds XML in memory. Expose it through `GET /api/emulators/{emulatorId}/dispatcher-template`, returning `404` when the emulator is missing and an XML file response when present.

**Tech Stack:** ASP.NET Core controller, EF Core, LINQ to XML, xUnit.

---

### Task 1: XML Generation Service

**Files:**
- Create: `UniEmu/Features/Emulators/DispatcherTemplateService.cs`
- Test: `UniEmu.Tests/Features/Emulators/DispatcherTemplateServiceTests.cs`
- Modify: `UniEmu/Hosting/UniEmuBackendServiceRegistration.cs`

- [ ] Write a failing xUnit test that seeds an emulator with bool, numeric, and string tags and asserts generated XML contains dispatcher fields, UTF-8 declaration, namespaces, special parameter numbers, and data type numbers.
- [ ] Run `dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore --filter DispatcherTemplateServiceTests -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True` and confirm it fails because `DispatcherTemplateService` does not exist.
- [ ] Implement `DispatcherTemplateService` with `CreateAsync(string emulatorId, CancellationToken)` returning `null` for missing emulator or `DispatcherTemplateFile` with filename/content for existing emulator.
- [ ] Register `DispatcherTemplateService` in Autofac.
- [ ] Re-run the filtered test and confirm it passes.

### Task 2: Download Endpoint

**Files:**
- Modify: `UniEmu/Controllers/EmulatorsController.cs`
- Test: `UniEmu.Tests/Features/Emulators/EmulatorsControllerTests.cs`

- [ ] Write a failing controller test that uses a mocked service result and asserts `GetDispatcherTemplate` returns a `FileContentResult` with `application/xml; charset=utf-8`, generated filename, and bytes.
- [ ] Implement the controller action `GET /api/emulators/{emulatorId}/dispatcher-template`.
- [ ] Re-run controller and service tests.

### Task 3: Verification

**Files:**
- All changed backend and test files.

- [ ] Run `dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True`.
- [ ] Inspect `git diff --stat` and `git diff --check`.
