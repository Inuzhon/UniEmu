# Scripting API Boundary Design

## Goal

Move the C# script-facing API into a small dedicated project so scripts and Roslyn IntelliSense can reference only the allowed user API instead of the whole `UniEmu` backend assembly.

## Architecture

Create `UniEmu.Scripting.Api`, a class library that contains only types intended for user scripts. The backend project references this API project and continues to execute scripts with the existing `CSharpScript` flow, compiled script cache, `#load` support, globals object, and runtime state handling.

The new scripting API namespace is `UniEmu.Scripting.Api`. Current script ergonomics remain familiar: scripts can use `Now`, `UniEmu.Tag`, `UniEmu.Tags`, `UniEmu.State`, and `UniEmu.Emulator`. The CLR type behind the `UniEmu` property is renamed to `UniEmuScriptContext` to avoid naming it the same as the application.

## API Surface

The new project owns:

- `TagScriptGlobals`
- `UniEmuScriptContext`
- `TagScriptValue`
- `TagScriptTagAccessor`
- `TagScriptStateContext`
- `TagScriptEmulatorContext`
- `TagScriptValueType`

`TagScriptValueType` replaces the script-facing dependency on `UniEmu.Contracts.Enums.TagType`, keeping the contract enum assembly out of script references.

## Runtime Flow

`TagScriptExecutionService` builds the same globals object as today, but from `UniEmu.Scripting.Api`. It maps internal `TagType` values to `TagScriptValueType` before constructing `TagScriptValue`.

`CompiledTagScriptCache` still caches `Script<object?>` instances and runs them through `RunAsync(globals)`. No DLL emit, no worker exe, and no out-of-process execution are introduced.

## Roslyn Flow

`CsxLanguageService` uses only BCL references plus `UniEmu.Scripting.Api` for script analysis, completions, hover, and signature help. Imports are updated from `UniEmu.Runtime.Scripting.UserScripts` to `UniEmu.Scripting.Api`.

The old commented path that could add the whole globals assembly references is removed or kept impossible to re-enable accidentally. Tests assert that metadata references do not include `UniEmu.dll`.

## Migration

Existing backend code is updated to import `UniEmu.Scripting.Api`. Existing user scripts that rely only on globals like `Now` and `UniEmu.Tags` continue to work. Scripts that explicitly reference the old namespace must switch to `UniEmu.Scripting.Api`.

## Testing

Add or update tests to prove:

- script analysis accepts the new `UniEmu` globals API;
- Roslyn metadata references include `UniEmu.Scripting.Api`;
- Roslyn metadata references do not include the backend `UniEmu` assembly;
- existing runtime script tag tests still pass.
