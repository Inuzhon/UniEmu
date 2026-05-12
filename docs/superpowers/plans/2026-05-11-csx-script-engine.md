# CSX Script Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a top-level `.csx` script engine workspace so UniEmu scripts keep in-memory execution while IntelliSense, completion, hover, signature help, and diagnostics understand the global `UniEmu` object and loaded scripts.

**Architecture:** Split the current large `CsxLanguageService` into a shared script environment, Roslyn workspace context, and small feature services. Runtime execution remains `CSharpScript` based and reuses shared helpers where this reduces drift. `CsxIntellisenseService` stays as the database-facing facade.

**Tech Stack:** .NET 10, Roslyn `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp.Scripting`, xUnit, EF Core SQLite test fixtures.

---

## File Structure

- Create `UniEmu/Runtime/Scripting/Environment/CsxScriptEnvironment.cs`
  - Owns imports, script options, parse options, compilation options, metadata reference caching.
- Create `UniEmu/Runtime/Scripting/Environment/CsxLoadedScriptExpander.cs`
  - Resolves `#load`, expands loaded scripts for Roslyn features, maps requested position into expanded source.
- Create `UniEmu/Runtime/Scripting/Environment/CsxScriptDirectiveValidator.cs`
  - Blocks unsupported directives and detects `#load` cycles.
- Create `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContext.cs`
  - Disposable wrapper for `AdhocWorkspace`, `Document`, source text, and mapped position.
- Create `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContextFactory.cs`
  - Creates script-kind Roslyn documents using the shared environment.
- Create `UniEmu/Runtime/Scripting/Common/CsxDocumentationFormatter.cs`
  - Formats XML documentation and Roslyn tagged text.
- Create `UniEmu/Runtime/Scripting/Common/CsxRoslynSymbolHelpers.cs`
  - Resolves hover/signature symbols and maps Roslyn tags to DTO kinds.
- Create `UniEmu/Runtime/Scripting/Common/CsxPositionMapper.cs`
  - Converts 1-based editor line/column positions to string offsets.
- Create `UniEmu/Runtime/Scripting/Services/CsxDiagnosticsService.cs`
- Create `UniEmu/Runtime/Scripting/Services/CsxCompletionService.cs`
- Create `UniEmu/Runtime/Scripting/Services/CsxHoverService.cs`
- Create `UniEmu/Runtime/Scripting/Services/CsxSignatureHelpService.cs`
- Modify `UniEmu/Runtime/Scripting/CsxLanguageService.cs`
  - Make it a small facade over the new services and keep existing public DTO records.
- Modify `UniEmu/Runtime/Scripting/CsxIntellisenseService.cs`
  - Use `CsxPositionMapper` and keep database loading behavior.
- Modify `UniEmu/Runtime/TagScriptExecutionService.cs`
  - Use `CsxScriptEnvironment` and `CsxScriptDirectiveValidator` for shared script rules.
- Modify `UniEmu/Runtime/CompiledTagScriptCache.cs`
  - Keep API compatible but use environment-provided script options when called.
- Modify `UniEmu/Program.cs`
  - Register new singleton Roslyn/context/service classes.
- Modify tests in `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`
- Modify tests in `UniEmu.Tests/Runtime/Scripting/CsxIntellisenseServiceTests.cs`
- Modify tests in `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`
- Modify tests in `UniEmu.Tests/Features/Tags/TagServiceScriptValidationTests.cs` only if constructor dependencies change.

---

### Task 1: Red Tests For Top-Level UniEmu IntelliSense

**Files:**
- Modify: `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`

- [ ] **Step 1: Add failing tests for global `UniEmu` completion and hover**

Add these tests to `CsxLanguageServiceTests`:

```csharp
[Fact]
public void GetCompletions_ReturnsUniEmuGlobalAtTopLevel()
{
    var service = new CsxLanguageService();

    var completions = service.GetCompletions(
        "inline/tag-1.csx",
        "Uni",
        3,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals));

    Assert.Contains(completions, item => item.Label == "UniEmu");
}

[Fact]
public void GetHover_ReturnsScriptingApiDocumentationForUniEmuTags()
{
    var service = new CsxLanguageService();
    const string content = "return UniEmu.Tags;";
    var position = content.IndexOf("Tags", StringComparison.Ordinal) + 1;

    var hover = service.GetHover(
        "inline/tag-1.csx",
        content,
        position,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals));

    Assert.NotNull(hover);
    Assert.Contains("Tags", hover.Signature, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests.GetCompletions_ReturnsUniEmuGlobalAtTopLevel|FullyQualifiedName~CsxLanguageServiceTests.GetHover_ReturnsScriptingApiDocumentationForUniEmuTags"
```

Expected: at least one test fails because the current Roslyn completion/hover setup does not reliably expose the globals object with feature metadata.

---

### Task 2: Extract Shared Script Environment

**Files:**
- Create: `UniEmu/Runtime/Scripting/Environment/CsxScriptEnvironment.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`

- [ ] **Step 1: Implement `CsxScriptEnvironment`**

Create `CsxScriptEnvironment` with:

```csharp
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptEnvironment
{
    private static readonly string[] s_imports =
    [
        "System",
        "System.Collections.Generic",
        "System.Globalization",
        "System.Linq",
        "UniEmu.Scripting.Api",
    ];

    private readonly ConcurrentDictionary<Type, IReadOnlyList<MetadataReference>> metadataReferenceCache = new();

    public CSharpParseOptions ParseOptions { get; } = CSharpParseOptions.Default
        .WithKind(SourceCodeKind.Script)
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithDocumentationMode(DocumentationMode.Diagnose);

    public CSharpCompilationOptions CompilationOptions { get; } = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithUsings(s_imports)
        .WithAllowUnsafe(false)
        .WithOptimizationLevel(OptimizationLevel.Debug);

    public ScriptOptions CreateScriptOptions(
        string entryPath,
        IReadOnlyDictionary<string, string> visibleScripts)
    {
        return ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(DateTimeOffset).Assembly,
                typeof(TagScriptGlobals).Assembly)
            .WithImports(s_imports)
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
    }

    public IReadOnlyList<MetadataReference> CreateMetadataReferences(Type globalsType)
    {
        return metadataReferenceCache.GetOrAdd(globalsType, static type =>
        {
            var references = new List<MetadataReference>();
            AddAssemblyReference(references, typeof(object).Assembly);
            AddAssemblyReference(references, typeof(Enumerable).Assembly);
            AddAssemblyReference(references, typeof(DateTimeOffset).Assembly);
            AddAssemblyReference(references, typeof(TagScriptGlobals).Assembly);
            AddAssemblyReference(references, type.Assembly);

            return references
                .DistinctBy(reference => reference.Display)
                .ToList();
        });
    }

    internal int MetadataReferenceCacheCount => metadataReferenceCache.Count;

    internal void ClearMetadataReferenceCacheForTests()
    {
        metadataReferenceCache.Clear();
    }

    private static void AddAssemblyReference(List<MetadataReference> references, System.Reflection.Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
    }
}
```

- [ ] **Step 2: Update `CsxLanguageService` constructor**

Add a constructor dependency:

```csharp
public sealed class CsxLanguageService(CsxScriptEnvironment environment)
```

Keep a parameterless constructor for existing tests:

```csharp
public CsxLanguageService()
    : this(new CsxScriptEnvironment())
{
}
```

- [ ] **Step 3: Replace static script options/reference helpers**

Replace uses of `s_baseOptions`, `CreateMetadataReferences`, and the static cache with `environment.CreateScriptOptions(...)` and `environment.CreateMetadataReferences(...)`.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: existing tests may still fail for the new red tests, but extraction should compile.

---

### Task 3: Add Loaded Script Expander And Directive Validator

**Files:**
- Create: `UniEmu/Runtime/Scripting/Environment/CsxLoadedScriptExpander.cs`
- Create: `UniEmu/Runtime/Scripting/Environment/CsxScriptDirectiveValidator.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`
- Modify: `UniEmu/Runtime/TagScriptExecutionService.cs`

- [ ] **Step 1: Create `CsxLoadedScriptExpander`**

Implement:

```csharp
using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxLoadedScriptExpander
{
    private static readonly Regex s_loadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public ExpandedCsxScript Expand(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts)
    {
        var prefix = new List<string>();
        foreach (Match match in s_loadDirective.Matches(content))
        {
            var loadPath = ResolveLoadPath(match.Groups["path"].Value, entryPath, visibleScripts);
            if (loadPath is null || !visibleScripts.TryGetValue(loadPath, out var loadedContent))
            {
                continue;
            }

            prefix.Add($"""#line 1 "{loadPath}"""");
            prefix.Add(loadedContent);
            prefix.Add("#line default");
        }

        if (prefix.Count == 0)
        {
            return new ExpandedCsxScript(content, Math.Clamp(position, 0, content.Length));
        }

        var prefixText = string.Join(Environment.NewLine, prefix) + Environment.NewLine;
        return new ExpandedCsxScript(prefixText + content, Math.Clamp(position, 0, content.Length) + prefixText.Length);
    }

    public string? ResolveLoadPath(
        string path,
        string baseFilePath,
        IReadOnlyDictionary<string, string> scripts)
    {
        var normalized = TagScriptPath.Normalize(path);
        if (scripts.ContainsKey(normalized))
        {
            return normalized;
        }

        var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var relative = TagScriptPath.Normalize($"{baseDir}/{path}");
        return scripts.ContainsKey(relative) ? relative : null;
    }

    public IEnumerable<string> GetLoadDirectivePaths(string content)
    {
        foreach (Match match in s_loadDirective.Matches(content))
        {
            yield return match.Groups["path"].Value;
        }
    }
}

public sealed record ExpandedCsxScript(string Content, int Position);
```

- [ ] **Step 2: Create `CsxScriptDirectiveValidator`**

Implement:

```csharp
using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptDirectiveValidator(CsxLoadedScriptExpander expander)
{
    private static readonly Regex s_blockedDirective = new(
        @"^\s*#\s*(r|using)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public void ValidateSupportedDirectives(string content)
    {
        var match = s_blockedDirective.Match(content);
        if (match.Success)
        {
            throw new InvalidOperationException($"Unsupported script directive '{match.Value.Trim()}'. Use #load for shared scripts.");
        }
    }

    public void DetectLoadCycles(string entryPath, IReadOnlyDictionary<string, string> scripts)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(TagScriptPath.Normalize(entryPath), visited, stack, scripts);
    }

    private void Visit(
        string path,
        HashSet<string> visited,
        HashSet<string> stack,
        IReadOnlyDictionary<string, string> scripts)
    {
        if (stack.Contains(path))
        {
            throw new InvalidOperationException($"Cyclic #load detected at '{path}'.");
        }

        if (!visited.Add(path) || !scripts.TryGetValue(path, out var content))
        {
            return;
        }

        stack.Add(path);
        foreach (var loadPathValue in expander.GetLoadDirectivePaths(content))
        {
            var loadPath = expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is not null)
            {
                Visit(loadPath, visited, stack, scripts);
            }
        }

        stack.Remove(path);
    }
}
```

- [ ] **Step 3: Wire helpers into `CsxLanguageService`**

Inject `CsxLoadedScriptExpander` and replace the old private `ExpandLoadedScripts`/`ResolveLoadPath`.

- [ ] **Step 4: Wire helpers into `TagScriptExecutionService`**

Inject `CsxScriptEnvironment` and `CsxScriptDirectiveValidator`. Replace local directive validation and cycle detection with shared validator calls.

- [ ] **Step 5: Run runtime scripting tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptExecutionServiceTests|FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: compile succeeds; the red IntelliSense tests can still fail until workspace services are added.

---

### Task 4: Add Roslyn Workspace Context

**Files:**
- Create: `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContext.cs`
- Create: `UniEmu/Runtime/Scripting/Workspace/CsxRoslynContextFactory.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`

- [ ] **Step 1: Implement `CsxRoslynContext`**

```csharp
using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime.Scripting.Workspace;

public sealed class CsxRoslynContext : IDisposable
{
    private readonly AdhocWorkspace workspace;
    private bool disposed;

    public CsxRoslynContext(AdhocWorkspace workspace, Document document, int position)
    {
        this.workspace = workspace;
        Document = document;
        Position = position;
    }

    public Document Document { get; }

    public int Position { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        workspace.Dispose();
        disposed = true;
    }
}
```

- [ ] **Step 2: Implement `CsxRoslynContextFactory`**

```csharp
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using UniEmu.Runtime.Scripting.Environment;

namespace UniEmu.Runtime.Scripting.Workspace;

public sealed class CsxRoslynContextFactory
{
    private readonly CsxScriptEnvironment environment;
    private readonly CsxLoadedScriptExpander expander;
    private readonly MefHostServices host;

    public CsxRoslynContextFactory(CsxScriptEnvironment environment, CsxLoadedScriptExpander expander)
    {
        this.environment = environment;
        this.expander = expander;

        var assemblies = MefHostServices.DefaultAssemblies
            .Concat(
            [
                typeof(CompletionService).Assembly,
                typeof(CSharpCompilation).Assembly,
                Assembly.Load("Microsoft.CodeAnalysis.Features"),
                Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"),
                Assembly.Load("Microsoft.CodeAnalysis.CSharp.Workspaces"),
                Assembly.Load("Microsoft.CodeAnalysis.Workspaces"),
            ])
            .Distinct();

        host = MefHostServices.Create(assemblies);
    }

    public CsxRoslynContext CreateContext(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        var expanded = expander.Expand(entryPath, content, position, visibleScripts);
        var workspace = new AdhocWorkspace(host);
        var projectId = ProjectId.CreateNewId("UniEmu.Csx");
        var documentId = DocumentId.CreateNewId(projectId, entryPath);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "UniEmu.Csx",
                "UniEmu.Csx",
                LanguageNames.CSharp,
                parseOptions: environment.ParseOptions,
                compilationOptions: environment.CompilationOptions,
                metadataReferences: environment.CreateMetadataReferences(globalsType)))
            .AddDocument(documentId, Path.GetFileName(entryPath), SourceText.From(expanded.Content));

        if (!workspace.TryApplyChanges(solution))
        {
            workspace.Dispose();
            throw new InvalidOperationException("Unable to apply CSX Roslyn solution changes.");
        }

        var document = workspace.CurrentSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("CSX Roslyn document was not created.");

        return new CsxRoslynContext(workspace, document, expanded.Position);
    }
}
```

- [ ] **Step 3: Replace document creation in `CsxLanguageService`**

Remove private `CreateDocument`. Call `contextFactory.CreateContext(...)` for each language feature.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"
```

Expected: global completion/hover tests should be closer to passing, but documentation/detail may still need service extraction.

---

### Task 5: Extract Feature Services And Improve Completion/Hover

**Files:**
- Create: `UniEmu/Runtime/Scripting/Common/CsxDocumentationFormatter.cs`
- Create: `UniEmu/Runtime/Scripting/Common/CsxRoslynSymbolHelpers.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxDiagnosticsService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxCompletionService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxHoverService.cs`
- Create: `UniEmu/Runtime/Scripting/Services/CsxSignatureHelpService.cs`
- Modify: `UniEmu/Runtime/Scripting/CsxLanguageService.cs`

- [ ] **Step 1: Create common helpers**

Move XML/tagged-text documentation formatting, completion kind mapping, symbol resolution, callable symbol resolution, and active parameter calculation out of `CsxLanguageService` into common helper classes.

- [ ] **Step 2: Implement `CsxDiagnosticsService`**

It should create a context, get the compilation diagnostics for the document/project, map them to `CsxDiagnostic`, and dispose the context.

- [ ] **Step 3: Implement `CsxCompletionService`**

It should call Roslyn `CompletionService.GetCompletionsAsync`, fetch `GetDescriptionAsync` for up to 100 items, and return `CsxCompletionItem` with label, sort/filter/insert text, detail, documentation, and kind.

- [ ] **Step 4: Implement `CsxHoverService`**

It should resolve the symbol at `context.Position`, return minimally qualified signature, formatted documentation, and token span offsets.

- [ ] **Step 5: Implement `CsxSignatureHelpService`**

It should find invocation/object creation argument lists, resolve overloads, and return `CsxSignatureHelp`.

- [ ] **Step 6: Make `CsxLanguageService` a facade**

Keep public methods and DTO records in `CsxLanguageService.cs`, but delegate implementation to the small services.

- [ ] **Step 7: Run focused tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests|FullyQualifiedName~CsxIntellisenseServiceTests"
```

Expected: all IntelliSense tests pass.

---

### Task 6: Register Dependencies And Fix Constructor Call Sites

**Files:**
- Modify: `UniEmu/Program.cs`
- Modify: `UniEmu.Tests/Features/Tags/TagServiceScriptValidationTests.cs`
- Modify: `UniEmu.Tests/Runtime/TagScriptExecutionServiceTests.cs`
- Modify: `UniEmu.Tests/Runtime/Scripting/CsxIntellisenseServiceTests.cs`

- [ ] **Step 1: Register new services in Autofac**

Register singletons:

```csharp
container.RegisterType<CsxScriptEnvironment>().AsSelf().SingleInstance();
container.RegisterType<CsxLoadedScriptExpander>().AsSelf().SingleInstance();
container.RegisterType<CsxScriptDirectiveValidator>().AsSelf().SingleInstance();
container.RegisterType<CsxRoslynContextFactory>().AsSelf().SingleInstance();
container.RegisterType<CsxDiagnosticsService>().AsSelf().SingleInstance();
container.RegisterType<CsxCompletionService>().AsSelf().SingleInstance();
container.RegisterType<CsxHoverService>().AsSelf().SingleInstance();
container.RegisterType<CsxSignatureHelpService>().AsSelf().SingleInstance();
```

- [ ] **Step 2: Update test constructors**

Where tests instantiate `TagScriptExecutionService` directly, pass the new dependencies or use helper constructors that create default environment/validator instances.

- [ ] **Step 3: Run compile-focused backend tests**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~TagScriptExecutionServiceTests|FullyQualifiedName~TagServiceScriptValidationTests|FullyQualifiedName~CsxIntellisenseServiceTests"
```

Expected: selected tests pass.

---

### Task 7: Full Verification And Cleanup

**Files:**
- Modify only files needed by compiler/test failures.

- [ ] **Step 1: Run full backend test suite**

Run:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True
```

Expected: exit code 0.

- [ ] **Step 2: Inspect changed files**

Run:

```powershell
git status --short
git diff --stat
```

Expected: only planned scripting/runtime/test files changed, plus the plan document.

- [ ] **Step 3: Commit implementation**

Run:

```powershell
git add UniEmu/Runtime/Scripting UniEmu/Runtime/TagScriptExecutionService.cs UniEmu/Runtime/CompiledTagScriptCache.cs UniEmu/Program.cs UniEmu.Tests docs/superpowers/plans/2026-05-11-csx-script-engine.md
git commit -m "feat: add top-level csx roslyn engine"
```

Expected: implementation commit created without staging unrelated `.gitignore`, `UniEmu.Client`, or `ByteFight` changes.

---

## Self-Review

- Spec coverage: top-level `.csx`, global `UniEmu`, completion, hover, diagnostics, signature help, loaded scripts, in-memory runtime execution, and split files are covered.
- Placeholder scan: no `TBD`, `TODO`, or deferred implementation steps are present.
- Type consistency: planned types use the `Csx*` prefix and live under `UniEmu.Runtime.Scripting` sub-namespaces; facade public DTOs remain in `CsxLanguageService.cs`.
