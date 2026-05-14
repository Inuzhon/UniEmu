# Monaco CSX Language Features Design

## Goal

Extend the CSX Monaco editor with Roslyn-backed navigation, references, call hierarchy, rename, formatting, folding, and semantic tokens while preserving the existing completion, hover, signature help, and diagnostics behavior.

## Scope

The feature covers `.csx` Monaco models created by `MonacoCsxEditor`. Backend analysis continues to use the current CSX Roslyn environment: script parse options, runtime-compatible references, `TagScriptGlobals`, and visible scripts loaded from the database according to script scope and emulator id.

Rename is intentionally local to the currently edited script. It may resolve symbols with Roslyn, but returned edits must only target the request document. References, navigation, type definition, implementation, and call hierarchy may inspect visible loaded scripts so Roslyn can understand the workspace.

## Backend Design

`IntellisenseController` gets additional `POST /api/intellisense/csharp/*` endpoints for:

- `definition`
- `type-definition`
- `references`
- `implementation`
- `call-hierarchy/prepare`
- `call-hierarchy/incoming`
- `call-hierarchy/outgoing`
- `rename`
- `format`
- `format-range`
- `folding-ranges`
- `semantic-tokens`

`CsxIntellisenseService` remains the application boundary. It loads visible scripts once per request and delegates to `CsxLanguageService`.

`CsxLanguageService` gains focused services:

- symbol navigation and references service;
- call hierarchy service;
- rename service limited to the request entry document;
- formatting service;
- folding range service;
- semantic token service.

Responses use editor-oriented DTOs with line/character positions and document URIs. For loaded scripts, locations can point to `uniemu://scripts/...` only when the backend can resolve a stored script identity; otherwise they can return the normalized path as a stable internal URI. Rename edits return only the current document URI.

## Frontend Design

`src/components/MonacoCsxEditor/` gets provider modules matching Monaco registrations:

- `navigation.ts`
- `references.ts`
- `implementation.ts`
- `callHierarchy.ts`
- `rename.ts`
- `formatting.ts`
- `folding.ts`
- `semanticTokens.ts`

`registerCsxIntellisense.ts` registers all providers once for `MONACO_LANGUAGE_ID`.

The shared request helper supports optional position, range, rename text, and custom payload fields. Mapping helpers convert backend ranges to Monaco ranges and locations. Empty or failed backend responses return `null` or empty arrays so Monaco gracefully falls back.

## Behavior

Definition and type definition jump to Roslyn-resolved source locations when available. Metadata-only symbols return no location.

References return source references from the CSX workspace. Implementation returns Roslyn implementations for interfaces and virtual members where Roslyn can resolve them.

Call hierarchy prepares an item at the cursor and supports incoming and outgoing calls. If Roslyn cannot build hierarchy for a symbol, the provider returns empty results.

Rename validates the new identifier on the backend and returns a `WorkspaceEdit` containing only text edits for the current Monaco model. Imported `#load` scripts are not edited.

Formatting uses Roslyn formatting for the full document and selected range. Folding ranges include syntax-driven blocks plus comment/region style ranges when available from Roslyn syntax.

Semantic tokens use Roslyn semantic classification, mapped to a stable Monaco legend. The regex tokenizer remains a fallback.

## Testing

Backend tests cover each Roslyn operation at `CsxLanguageService` level and at least one `CsxIntellisenseService` test for DB-backed visible scripts. Rename tests explicitly prove loaded script files are not edited.

Frontend source tests verify provider registration and request mapping because the current project uses lightweight source tests rather than a browser Monaco test harness.
