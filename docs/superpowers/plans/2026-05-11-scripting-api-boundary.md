# Scripting API Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move script-visible C# API types into `UniEmu.Scripting.Api` while preserving current in-process `CSharpScript` execution.

**Architecture:** Add a small class library referenced by `UniEmu` and `UniEmu.Tests`. Move only script API types into it, replace script-facing `TagType` with `TagScriptValueType`, and update runtime/Roslyn imports and references to point at the new assembly.

**Tech Stack:** .NET 10 preview, Roslyn CSharp scripting/features, xUnit.

---

## File Structure

- Create `UniEmu.Scripting.Api/UniEmu.Scripting.Api.csproj`: standalone script API library.
- Create `UniEmu.Scripting.Api/*.cs`: script-visible globals, context, values, accessors, and enum.
- Modify `UniEmu.slnx`: include the new project.
- Modify `UniEmu/UniEmu.csproj`: reference the API project.
- Modify `UniEmu.Tests/UniEmu.Tests.csproj`: reference the API project.
- Modify `UniEmu/Runtime/TagScriptExecutionService.cs`: construct API types and map internal tag types.
- Modify `UniEmu/Runtime/Scripting/CsxLanguageService.cs`: imports/references use API assembly only.
- Modify `UniEmu/Runtime/Scripting/CsxIntellisenseService.cs`, `UniEmu/Features/Tags/TagService.cs`: update namespaces.
- Delete old `UniEmu/Runtime/Scripting/UserScripts/*.cs` after consumers move.
- Test `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`: add boundary tests.

### Task 1: Failing Boundary Tests

- [ ] Add tests in `UniEmu.Tests/Runtime/Scripting/CsxLanguageServiceTests.cs`:

```csharp
[Fact]
public void Analyze_AcceptsScriptGlobalsFromScriptingApi()
{
    var service = new CsxLanguageService();

    var result = service.Analyze(
        "inline/tag-1.csx",
        "return UniEmu.Tag.Type == TagScriptValueType.Double ? Now.Offset.TotalHours : 0;",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        typeof(TagScriptGlobals));

    Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
}

[Fact]
public void CreateMetadataReferences_ExposesScriptingApiWithoutBackendAssembly()
{
    var references = CsxLanguageService.CreateMetadataReferencesForTests(typeof(TagScriptGlobals));
    var displays = references.Select(reference => Path.GetFileName(reference.Display ?? string.Empty)).ToList();

    Assert.Contains("UniEmu.Scripting.Api.dll", displays);
    Assert.DoesNotContain("UniEmu.dll", displays);
}
```

- [ ] Add `using Microsoft.CodeAnalysis;` and `using UniEmu.Scripting.Api;` if needed.
- [ ] Run: `$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'; dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True --filter "FullyQualifiedName~CsxLanguageServiceTests"`.
- [ ] Expected: fail because `UniEmu.Scripting.Api`, `TagScriptValueType`, and `CreateMetadataReferencesForTests` do not exist yet.

### Task 2: Add API Project

- [ ] Create `UniEmu.Scripting.Api/UniEmu.Scripting.Api.csproj` targeting `net10.0`.
- [ ] Move script API code into `UniEmu.Scripting.Api` namespace with `TagScriptValue.Type` using `TagScriptValueType`.
- [ ] Add XML comments already present on user-facing members.
- [ ] Add the project to `UniEmu.slnx`.
- [ ] Reference it from `UniEmu/UniEmu.csproj` and `UniEmu.Tests/UniEmu.Tests.csproj`.
- [ ] Run the same filtered tests.
- [ ] Expected: remaining compile failures point to old namespaces and missing Roslyn reference helper.

### Task 3: Update Runtime And Roslyn

- [ ] Update `TagScriptExecutionService` to `using UniEmu.Scripting.Api`.
- [ ] Add private mapper:

```csharp
private static TagScriptValueType ToScriptValueType(TagType type) => type switch
{
    TagType.Bool => TagScriptValueType.Bool,
    TagType.Int => TagScriptValueType.Int,
    TagType.Double => TagScriptValueType.Double,
    TagType.String => TagScriptValueType.String,
    _ => TagScriptValueType.String,
};
```

- [ ] Use mapped script type for every `new TagScriptValue(...)`.
- [ ] Update `CsxLanguageService.s_baseOptions` imports to `UniEmu.Scripting.Api`.
- [ ] Make metadata references include `typeof(TagScriptGlobals).Assembly` and expose an internal `CreateMetadataReferencesForTests(Type globalsType)` wrapper.
- [ ] Remove old `UserScripts` source files after all references move.
- [ ] Run the filtered tests.
- [ ] Expected: `CsxLanguageServiceTests` pass.

### Task 4: Full Verification

- [ ] Run: `$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'; dotnet test UniEmu.Tests\UniEmu.Tests.csproj --no-restore -nr:false -p:UseSharedCompilation=false -p:SkipYarnBuild=True`.
- [ ] Expected: all tests pass, aside from the known .NET preview SDK warning.
- [ ] Inspect `git diff --stat` and ensure no unrelated dirty files were changed.
