# CSX Script Engine Design

## Goal

UniEmu scripts remain top-level `.csx` scripts executed in memory, while IntelliSense, diagnostics, hover, and signature help use a proper Roslyn workspace model inspired by ByteFight's `UserScriptRoslynContextFactory` and related services.

The editor and runtime must agree on the available API: scripts can access the global `UniEmu` object from `TagScriptGlobals`, standard imports, shared/emulator scripts through `#load`, and the scripting API assembly. Scripts are not compiled to a file or DLL as part of normal execution.

## Chosen Approach

Use a shared top-level CSX environment plus small Roslyn service classes.

This keeps runtime execution on `CSharpScript.Create(...).RunAsync(...)`, but moves editor features to a dedicated workspace layer:

- `CsxRoslynContextFactory` creates an `AdhocWorkspace`, project, and document for one script request.
- `CsxRoslynContext` owns the workspace/document and position mapping.
- Small IntelliSense services operate on that context: diagnostics, completion, hover, and signature help.
- A shared environment/helper owns parse options, compilation options, imports, metadata references, and script source expansion rules.

This avoids ByteFight's method/class wrapper because UniEmu scripts are top-level scripts. Roslyn documents use `SourceCodeKind.Script` and references/imports match runtime execution.

## Architecture

New code lives under `UniEmu/Runtime/Scripting`, grouped similarly to the reference project:

- `Environment/`
  - `CsxScriptEnvironment`: shared imports, metadata references, parse options, compilation options, and script options.
  - `CsxLoadedScriptExpander`: resolves `#load`, expands visible scripts for Roslyn, detects/directs position mapping.
  - `CsxScriptDirectiveValidator`: blocks unsupported directives such as `#r` and unsupported `#using`, while allowing `#load`.
- `Workspace/`
  - `CsxRoslynContextFactory`: creates the workspace/project/document.
  - `CsxRoslynContext`: disposable wrapper around workspace and document.
- `Services/`
  - `CsxDiagnosticsService`
  - `CsxCompletionService`
  - `CsxHoverService`
  - `CsxSignatureHelpService`
- `Common/`
  - `CsxRoslynMappingHelpers`
  - `CsxDocumentationFormatter`
  - optional symbol/completion mapping helpers.

`CsxIntellisenseService` remains the database-facing facade. It parses document URI, loads visible scripts, builds the entry path, converts editor positions, and delegates to the small Roslyn services.

`TagScriptExecutionService` continues to execute scripts in memory. It should use shared environment/directive/load helpers where practical so runtime and IntelliSense stay aligned.

## Data Flow

For IntelliSense:

1. Controller receives source code, document URI, and optional position.
2. `CsxIntellisenseService` parses the URI and loads shared plus emulator-visible scripts from EF/cache.
3. The requested unsaved document content replaces the stored content for the entry path.
4. `CsxRoslynContextFactory` creates a script-kind Roslyn document with:
   - expanded `#load` content for symbols from visible scripts;
   - `TagScriptGlobals` as the script globals type;
   - references/imports matching runtime script execution.
5. Feature-specific services return diagnostics, completions, hover, or signature help DTOs.

For execution:

1. `TagScriptExecutionService` resolves inline or stored entry script.
2. It validates directives, loads visible scripts, detects `#load` cycles, builds `TagScriptGlobals`, and runs the script through cached `CSharpScript`.
3. It persists dirty script state and returns the tag value as today.

## Behavior

Scripts keep top-level syntax:

```csharp
#load "common.csx"

var value = UniEmu.Tags["pressure"].Value;
return value;
```

Expected editor behavior:

- `UniEmu` appears in completion at top level.
- `UniEmu.Tags`, `UniEmu.Tag`, `UniEmu.State`, and API types expose completion and hover.
- Symbols declared in loaded scripts appear in completion and resolve in diagnostics.
- Hover returns useful Roslyn signatures/documentation when available.
- Signature help works for framework methods and scripting API methods.
- Diagnostics use the same references/imports as runtime.

## Error Handling

- Oversized source requests remain rejected by the controller.
- Unsupported directives produce clear diagnostics or validation errors.
- Missing loaded scripts should not crash IntelliSense; unresolved symbols show regular diagnostics.
- Cyclic `#load` remains a runtime validation error and should be covered by shared load traversal where practical.
- Roslyn workspace creation failures throw clear `InvalidOperationException` messages.

## Testing

Use test-first implementation.

Required backend tests:

- completion includes `UniEmu` and scripting API members in top-level `.csx`;
- hover resolves `UniEmu.Tags` or another `TagScriptGlobals` member;
- diagnostics accept valid `UniEmu.*` usage with `TagScriptGlobals`;
- completion and diagnostics see functions from `#load` scripts;
- runtime still executes top-level scripts and loaded scripts in memory;
- metadata reference caching still exposes `UniEmu.Scripting.Api.dll` without pulling the backend assembly into the scripting API surface.

Existing tests around script execution, script validation, and IntelliSense should continue to pass.

## Scope

In scope:

- backend Roslyn/intellisense refactor;
- shared script environment helpers;
- runtime alignment with shared helpers where low-risk;
- focused unit tests.

Out of scope:

- compiling user scripts to physical files or emitted DLLs;
- changing frontend editor protocol;
- redesigning script storage;
- sandboxing beyond the existing directive/reference restrictions.
