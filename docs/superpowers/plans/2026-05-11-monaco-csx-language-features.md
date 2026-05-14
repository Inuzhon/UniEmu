# Monaco CSX Language Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Roslyn-backed Monaco language providers for CSX scripts.

**Architecture:** Extend the existing IntellisenseController -> CsxIntellisenseService -> CsxLanguageService chain with focused Roslyn operations. Keep Monaco provider files thin and map backend DTOs to Monaco-native provider return types.

**Tech Stack:** ASP.NET Core, Roslyn Workspaces/Features, xUnit, React, Monaco Editor 0.55, TypeScript.

---

### Task 1: Backend DTOs And Roslyn Context Support

**Files:**
- Modify: `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContext.cs`
- Modify: `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContextFactory.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`
- Test: `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`

- [ ] Add a failing test proving definition can resolve a local method location.
- [ ] Run `dotnet test UniEmu.Tests/UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"` and confirm the new test fails because the API does not exist.
- [ ] Add source document metadata to `CsxRoslynContext` so backend operations can map Roslyn locations back to normalized CSX paths.
- [ ] Add DTO records for text ranges, locations, text edits, workspace edits, call hierarchy items, folding ranges, and semantic tokens.
- [ ] Add `CsxLanguageService` methods that delegate to new service classes.
- [ ] Re-run the filtered test and confirm it passes.

### Task 2: Backend Navigation, References, Implementation

**Files:**
- Create: `UniEmu/Runtime/Scripting/Services/CsxNavigationService.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`
- Test: `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`

- [ ] Add failing tests for definition, type definition, references, and implementation.
- [ ] Verify the tests fail because the operations return empty or do not exist.
- [ ] Implement Roslyn symbol resolution, `SymbolFinder.FindReferencesAsync`, and source location mapping.
- [ ] Re-run the filtered backend tests and confirm they pass.

### Task 3: Backend Rename, Formatting, Folding, Semantic Tokens

**Files:**
- Create: `UniEmu/Runtime/Scripting/Services/CsxRenameService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxFormattingService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxFoldingService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxSemanticTokensService.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`
- Test: `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`

- [ ] Add failing tests for local-only rename, full formatting, range formatting, folding ranges, and semantic tokens.
- [ ] Verify the tests fail for missing behavior.
- [ ] Implement rename with edits filtered to the entry document.
- [ ] Implement Roslyn formatting and syntax folding.
- [ ] Implement semantic token classification with a stable token legend.
- [ ] Re-run the filtered backend tests and confirm they pass.

### Task 4: Backend Intellisense Endpoints

**Files:**
- Modify: `UniEmu/Controllers/IntellisenseController.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxIntellisenseService.cs`
- Test: `UniEmu.Tests/Runtime/Scripting/CsxIntellisenseServiceTests.cs`

- [ ] Add failing service tests for a loaded-script reference and local-only rename through `CsxIntellisenseService`.
- [ ] Verify the tests fail because service methods do not exist.
- [ ] Add service methods and controller endpoints for all new providers.
- [ ] Keep source length validation and position clamping behavior consistent with existing endpoints.
- [ ] Re-run intellisense tests and confirm they pass.

### Task 5: Frontend Monaco Providers

**Files:**
- Modify: `UniEmu.Client/src/components/MonacoCsxEditor/request.ts`
- Modify: `UniEmu.Client/src/components/MonacoCsxEditor/types.ts`
- Modify: `UniEmu.Client/src/components/MonacoCsxEditor/registerCsxIntellisense.ts`
- Create: provider modules under `UniEmu.Client/src/components/MonacoCsxEditor/`
- Test: `UniEmu.Client/src/components/MonacoCsxEditor.lsp-source.test.mjs`

- [ ] Add source tests asserting every requested Monaco registration is present.
- [ ] Verify the source tests fail because providers are missing.
- [ ] Add TypeScript DTOs and request payload support for ranges, rename text, and call hierarchy items.
- [ ] Implement provider modules and register them once.
- [ ] Run the source test and confirm it passes.

### Task 6: Verification

**Files:**
- No new files.

- [ ] Run backend filtered tests for scripting.
- [ ] Run frontend source tests.
- [ ] Run backend build with `SkipYarnBuild=True`.
- [ ] Run frontend build if dependencies are already installed.
- [ ] Summarize limitations, especially metadata navigation and local-only rename.
