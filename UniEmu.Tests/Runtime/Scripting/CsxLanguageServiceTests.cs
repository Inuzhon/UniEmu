using System.Diagnostics;
using UniEmu.Scripting.Api;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Services;
using Xunit.Abstractions;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxLanguageServiceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Class", "class")]
    [InlineData("Constant", "constant")]
    [InlineData("Delegate", "function")]
    [InlineData("Enum", "enum")]
    [InlineData("EnumMember", "enumMember")]
    [InlineData("Event", "event")]
    [InlineData("ExtensionMethod", "method")]
    [InlineData("Field", "field")]
    [InlineData("Interface", "interface")]
    [InlineData("Keyword", "keyword")]
    [InlineData("Label", "reference")]
    [InlineData("Local", "variable")]
    [InlineData("Method", "method")]
    [InlineData("Module", "module")]
    [InlineData("Namespace", "module")]
    [InlineData("Operator", "operator")]
    [InlineData("Parameter", "variable")]
    [InlineData("Property", "property")]
    [InlineData("RangeVariable", "variable")]
    [InlineData("Struct", "struct")]
    [InlineData("TypeParameter", "typeParameter")]
    public void GetCompletionKind_MapsRoslynTagsToEditorKinds(string roslynTag, string expectedKind)
    {
        var kind = CsxRoslynSymbolHelpers.GetCompletionKind([roslynTag]);

        Assert.Equal(expectedKind, kind);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCompilerErrorDiagnostic_WhenScriptReferencesUnknownIdentifier()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "return MissingValue;",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CS0103");
    }

    [Fact]
    public async Task AnalyzeAsync_UsesLoadedScriptContent_WhenEntryScriptHasLoadDirective()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags/shared/math.csx"] = "int Add(int a, int b) => a + b;",
        };

        var result = await service.AnalyzeAsync(
            "tags/tag-1.csx",
            "#load \"shared/math.csx\"\nreturn Add(1, 2);",
            visibleScripts);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("#r \"System.Text.Json.dll\"\nreturn 0;")]
    [InlineData("#r \"nuget: Newtonsoft.Json, 13.0.3\"\nreturn 0;")]
    public async Task AnalyzeAsync_ReturnsDirectiveErrorDiagnostic_WhenScriptUsesReferenceDirective(string content)
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CSX001");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsDirectiveErrorDiagnostic_WhenLoadedScriptUsesReferenceDirective()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["shared/references.csx"] = "#r \"System.Text.Json.dll\"\nint LoadedValue() => 1;",
        };

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "#load \"shared/references.csx\"\nreturn LoadedValue();",
            visibleScripts,
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CSX001");
    }

    [Fact]
    public async Task AnalyzeAsync_AcceptsScriptGlobalsFromScriptingApi()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "return UniEmu.Tag.Type == TagScriptValueType.Double ? Now.Offset.TotalHours : 0;",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSecurityDiagnostic_WhenScriptCallsForbiddenApi()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "return System.IO.File.ReadAllText(\"secret.txt\");",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "SEC003");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSecurityDiagnostic_WhenLoadedScriptUsesForbiddenType()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["shared/unsafe.csx"] = "var client = new System.Net.Http.HttpClient();",
        };

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "#load \"shared/unsafe.csx\"\nreturn 0;",
            visibleScripts,
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "SEC002");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSecurityDiagnostic_WhenScriptUsesUnsafeCode()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "unsafe { int value = 1; int* pointer = &value; return *pointer; }",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "SEC001");
    }

    [Fact]
    public async Task AnalyzeAsync_AllowsPublicTopLevelHelperMethod()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["utils.csx"] = "int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);",
        };
        const string content = """
            #load "utils.csx"
            public int Test(int qweqe)
            {
                return Clamp(123, 123, qweqe);
            }

            Test(123);
            return 0;
            """;

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            content,
            visibleScripts,
            typeof(TagScriptGlobals));

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCompilerErrorDiagnostic_WhenScriptReturnBranchesDoNotMatchExpectedType()
    {
        var service = new CsxLanguageService();
        const string content = """
            if (UniEmu.Tags.TryGetValue("NumericTag", out var numericTag)) {
                return numericTag;
            }

            return -1;
            """;

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals),
            typeof(int));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCompilerErrorDiagnostic_WhenExpectedReturnTypeDoesNotMatchScriptResult()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "return \"not an int\";",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals),
            typeof(int));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CS0029");
    }

    [Fact]
    public void CreateMetadataReferences_ExposesScriptingApiWithoutBackendAssembly()
    {
        var references = CsxLanguageService.CreateMetadataReferencesForTests(typeof(TagScriptGlobals));
        var displays = references
            .Select(reference => Path.GetFileName(reference.Display ?? string.Empty))
            .ToList();

        Assert.Contains("UniEmu.Scripting.Api.dll", displays);
        Assert.DoesNotContain("UniEmu.dll", displays);
    }

    [Fact]
    public void CreateMetadataReferences_UsesSmallScriptHostReferenceSet()
    {
        var references = CsxLanguageService.CreateMetadataReferencesForTests(typeof(TagScriptGlobals));
        var displays = references
            .Select(reference => Path.GetFileName(reference.Display ?? string.Empty))
            .ToList();

        Assert.True(displays.Count <= 32, $"Expected a compact reference set, got {displays.Count}: {string.Join(", ", displays.Order())}");
        Assert.DoesNotContain("Microsoft.AspNetCore.Mvc.Core.dll", displays);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.dll", displays);
        Assert.DoesNotContain("Quartz.dll", displays);
        Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp.Workspaces.dll", displays);
    }

    [Fact]
    public void CreateScriptOptions_UsesResolvedMetadataReferencesForSingleFilePublish()
    {
        var environment = new CsxScriptEnvironment();
        var options = environment.CreateScriptOptions(
            "inline/tag-1.csx",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var expectedDisplays = environment
            .CreateMetadataReferences(typeof(TagScriptGlobals))
            .Select(reference => reference.Display)
            .Order(StringComparer.Ordinal)
            .ToList();
        var actualDisplays = options.MetadataReferences
            .Select(reference => reference.Display)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expectedDisplays, actualDisplays);
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsSymbolsFromLoadedScripts()
    {
        CsxLanguageService.ClearMetadataReferenceCacheForTests();
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math.csx"] = "double LoadedHelper(double value) => value * 2;",
        };
        const string content = "#load \"math.csx\"\nreturn Load";

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            content,
            content.Length,
            visibleScripts);

        Assert.Contains(completions, item => item.Label == "LoadedHelper");
        var cacheCount = CsxLanguageService.MetadataReferenceCacheCount;
        Assert.True(cacheCount >= 1);

        _ = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            content,
            content.Length,
            visibleScripts);

        Assert.Equal(cacheCount, CsxLanguageService.MetadataReferenceCacheCount);
    }

    [Fact]
    public async Task LanguageFeatures_WorkWithUsingDirectiveImports()
    {
        var service = new CsxLanguageService();
        const string content = """
            using System.Text;

            var builder = new StringBuilder();
            builder.Append("Uni");
            builder.Append("Emu");
            var timestamp = Now;
            return $"{builder}:{timestamp.Offset.TotalHours}";
            """;

        var analysis = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals),
            typeof(string));
        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "using System.Text;\n\nvar builder = new StringBuil",
            "using System.Text;\n\nvar builder = new StringBuil".Length,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));
        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            content.IndexOf("Now", StringComparison.Ordinal) + 1,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));
        var signatureHelp = await service.GetSignatureHelpAsync(
            "inline/tag-1.csx",
            "using System.Text;\n\nvar value = Math.Round(",
            "using System.Text;\n\nvar value = Math.Round(".Length,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.DoesNotContain(analysis.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
        Assert.Contains(completions, item => item.Label == "StringBuilder");
        Assert.NotNull(hover);
        Assert.Contains("DateTimeOffset", hover.Signature, StringComparison.Ordinal);
        Assert.NotNull(signatureHelp);
        Assert.Contains(signatureHelp.Signatures, signature => signature.Label.Contains("Round", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("LoadedHelper", new[] { "method" }, false)]
    [InlineData("Now", new[] { "property" }, true)]
    [InlineData("DateTime", new[] { "class" }, false)]
    [InlineData("TagScriptValue", new[] { "class" }, true)]
    [InlineData("UniEmu.Scripting.Api", new[] { "namespace" }, true)]
    public void RequiresDescriptionForVisibility_OnlyKeepsDocumentationGateWhereNeeded(
        string label,
        string[] tags,
        bool expected)
    {
        Assert.Equal(expected, CsxCompletionService.RequiresDescriptionForVisibility(label, tags));
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsUniEmuGlobalAtTopLevel()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "Uni",
            3,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "UniEmu");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsMembersForUniEmuGlobal()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "UniEmu.",
            7,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "Tag");
        Assert.Contains(completions, item => item.Label == "Tags");
        Assert.Contains(completions, item => item.Label == "State");
        Assert.Contains(completions, item => item.Label == "Emulator");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsRestForUniEmuGlobal()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "UniEmu.",
            7,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "Rest");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsRestOperationMembers()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "UniEmu.Rest.",
            12,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "GetWorkerByIdAsync");
        Assert.Contains(completions, item => item.Label == "GetActiveWorkerAsync");
        Assert.Contains(completions, item => item.Label == "RegisterWorkerAsync");
        Assert.Contains(completions, item => item.Label == "TryRegisterWorkerAsync");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsScriptingApiEnumMembers()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "TagScriptValueType.",
            19,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "Bool");
        Assert.Contains(completions, item => item.Label == "Int");
        Assert.Contains(completions, item => item.Label == "Double");
        Assert.Contains(completions, item => item.Label == "String");
        Assert.All(completions.Where(item => item.Label is "Bool" or "Int" or "Double" or "String"),
            item => Assert.Equal("enumMember", item.Kind));
    }

    [Fact]
    public async Task AnalyzeAsync_AcceptsAwaitedRestOperationAndWorkerMembers()
    {
        var service = new CsxLanguageService();
        const string content = """
            var worker = await UniEmu.Rest.GetWorkerByIdAsync(123);
            return worker is not null && worker.IsActive
                ? worker.Id
                : -1;
            """;

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals),
            typeof(int));

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsRestContextConstructionFromScript()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "return new TagScriptRestContext(null!);",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsRestOperationsPortFromScript()
    {
        var service = new CsxLanguageService();

        var result = await service.AnalyzeAsync(
            "inline/tag-1.csx",
            "ITagScriptRestOperations rest = null!; return 0;",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals),
            typeof(int));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsMarkedScriptingApiMembers()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "UniEmu.State.",
            13,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(completions, item => item.Label == "Get");
        Assert.Contains(completions, item => item.Label == "Set");
    }

    [Fact]
    public async Task GetCompletionsAsync_DoesNotExposeRemovedTopLevelTagAlias()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "Tag.",
            4,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Empty(completions);
    }

    [Fact]
    public async Task GetCompletionsAsync_DoesNotReturnAllReferencedAssemblyTypesAtTopLevel()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "A",
            1,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.DoesNotContain(completions, item => item.Label == "AccessViolationException");
        Assert.DoesNotContain(completions, item => item.Label == "Activator");
    }

    [Fact]
    public async Task GetCompletionsAsync_HidesUnmarkedScriptingApiSymbols()
    {
        var service = new CsxLanguageService();

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "Scripting",
            9,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.DoesNotContain(completions, item => item.Label == "ScriptingApiAttribute");
    }

    [Fact]
    public async Task GetCompletionsAsync_RanksScriptSpecificSymbolsBeforeApiAndSystemSymbols()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math.csx"] = "double LoadedHelper(double value) => value * 2;",
        };

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "#load \"math.csx\"\n",
            "#load \"math.csx\"\n".Length,
            visibleScripts,
            typeof(TagScriptGlobals));

        AssertPrecedes(completions, "UniEmu", "LoadedHelper");
        AssertPrecedes(completions, "Now", "LoadedHelper");
        AssertPrecedes(completions, "LoadedHelper", "TagScriptValue");
        AssertPrecedes(completions, "TagScriptValue", "DateTime");
    }

    [Fact]
    public async Task GetCompletionsAsync_EmitsSortTextThatPreservesApiRankingInMonaco()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math.csx"] = "double LoadedHelper(double value) => value * 2;",
        };

        var completions = await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            "#load \"math.csx\"\n",
            "#load \"math.csx\"\n".Length,
            visibleScripts,
            typeof(TagScriptGlobals));

        AssertSortTextPrecedes(completions, "UniEmu", "LoadedHelper");
        AssertSortTextPrecedes(completions, "Now", "LoadedHelper");
        AssertSortTextPrecedes(completions, "LoadedHelper", "TagScriptValue");
        AssertSortTextPrecedes(completions, "TagScriptValue", "DateTime");
        AssertSortTextPrecedes(completions, "TagScriptValue", "double");
    }

    [Fact]
    public async Task LanguageFeatures_CompleteWithinWarmPathBudget()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["shared/math.csx"] = "double LoadedHelper(double value) => value * 2;",
        };
        const string content = """
            #load "shared/math.csx"

            var value = LoadedHelper(Math.Abs(-12.5));
            return UniEmu.Tags.TryGetValue("pressure", out var pressure)
                ? value
                : 0;
            """;

        await service.GetCompletionsAsync(
            "inline/tag-1.csx",
            content,
            content.IndexOf("UniEmu.", StringComparison.Ordinal) + "UniEmu.".Length,
            visibleScripts,
            typeof(TagScriptGlobals));

        var timings = new[]
        {
            await MeasureAsync("diagnostics", () => service.AnalyzeAsync(
                "inline/tag-1.csx",
                content,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("completions", () => service.GetCompletionsAsync(
                "inline/tag-1.csx",
                content,
                content.IndexOf("UniEmu.", StringComparison.Ordinal) + "UniEmu.".Length,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("hover", () => service.GetHoverAsync(
                "inline/tag-1.csx",
                content,
                content.IndexOf("TryGetValue", StringComparison.Ordinal) + 2,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("signature-help", () => service.GetSignatureHelpAsync(
                "inline/tag-1.csx",
                "var value = Math.Round(",
                "var value = Math.Round(".Length,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("definition", () => service.GetDefinitionsAsync(
                "inline/tag-1.csx",
                content,
                content.IndexOf("LoadedHelper", StringComparison.Ordinal) + 2,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("references", () => service.GetReferencesAsync(
                "inline/tag-1.csx",
                content,
                content.IndexOf("value", StringComparison.Ordinal) + 2,
                includeDeclaration: true,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("semantic-tokens", () => service.GetSemanticTokensAsync(
                "inline/tag-1.csx",
                content,
                visibleScripts,
                typeof(TagScriptGlobals))),
            await MeasureAsync("folding-ranges", () => service.GetFoldingRangesAsync(
                "inline/tag-1.csx",
                content,
                visibleScripts,
                typeof(TagScriptGlobals))),
        };

        foreach (var timing in timings)
        {
            output.WriteLine($"{timing.Name}: {timing.Elapsed.TotalMilliseconds:N0} ms");
        }

        Assert.All(timings, timing =>
            Assert.True(
                timing.Elapsed < TimeSpan.FromSeconds(2),
                $"{timing.Name} took {timing.Elapsed.TotalMilliseconds:N0} ms."));
    }

    [Fact]
    public async Task GetHoverAsync_ReturnsSymbolSignature()
    {
        var service = new CsxLanguageService();
        const string content = "var value = Math.Round(1.2);";
        var position = content.IndexOf("Round", StringComparison.Ordinal) + 2;

        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(hover);
        Assert.Contains("Round", hover.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHoverAsync_ReturnsSystemMethodDocumentation()
    {
        var service = new CsxLanguageService();
        const string content = "var value = Math.Abs(-1);";
        var position = content.IndexOf("Abs", StringComparison.Ordinal) + 1;

        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(hover);
        Assert.Contains("Abs", hover.Signature, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(hover.Documentation));
        Assert.Contains("absolute value", hover.Documentation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHoverAsync_ReturnsScriptingApiSymbolForUniEmuTags()
    {
        var service = new CsxLanguageService();
        const string content = "return UniEmu.Tags;";
        var position = content.IndexOf("Tags", StringComparison.Ordinal) + 1;

        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.NotNull(hover);
        Assert.Contains("Tags", hover.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHoverAsync_ReturnsScriptingApiSummaryDocumentation()
    {
        var service = new CsxLanguageService();
        const string content = "return UniEmu.Tags.TryGetValue(\"pressure\", out var pressure);";
        var position = content.IndexOf("TryGetValue", StringComparison.Ordinal) + 2;

        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.NotNull(hover);
        Assert.False(string.IsNullOrWhiteSpace(hover.Documentation));
    }

    [Fact]
    public async Task GetHoverAsync_PreservesLangwordDocumentationReferences()
    {
        var service = new CsxLanguageService();
        const string content = "return UniEmu.Tags.TryGetValue(\"pressure\", out var pressure);";
        var position = content.IndexOf("TryGetValue", StringComparison.Ordinal) + 2;

        var hover = await service.GetHoverAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.NotNull(hover);
        Assert.Contains("null", hover.Documentation, StringComparison.Ordinal);
        Assert.Contains("true", hover.Documentation, StringComparison.Ordinal);
        Assert.Contains("false", hover.Documentation, StringComparison.Ordinal);
        Assert.DoesNotContain("или ,", hover.Documentation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSignatureHelpAsync_ReturnsMethodParameters()
    {
        var service = new CsxLanguageService();
        const string content = "var value = Math.Round(";

        var signatureHelp = await service.GetSignatureHelpAsync(
            "inline/tag-1.csx",
            content,
            content.Length,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(signatureHelp);
        Assert.NotEmpty(signatureHelp.Signatures);
        Assert.Contains(signatureHelp.Signatures, signature =>
            signature.Label.Contains("Round", StringComparison.Ordinal)
            && signature.Parameters.Count > 0);
    }

    [Fact]
    public async Task GetDefinitionsAsync_ReturnsLocalMethodDeclarationLocation()
    {
        var service = new CsxLanguageService();
        const string content = """
            int Add(int left, int right) => left + right;
            return Add(1, 2);
            """;
        var position = content.LastIndexOf("Add", StringComparison.Ordinal) + 1;

        var definitions = await service.GetDefinitionsAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        var definition = Assert.Single(definitions);
        Assert.Equal("inline/tag-1.csx", definition.DocumentPath);
        Assert.Equal(0, definition.Range.StartLine);
        Assert.Equal(4, definition.Range.StartCharacter);
        Assert.Equal(0, definition.Range.EndLine);
        Assert.Equal(7, definition.Range.EndCharacter);
    }

    [Fact]
    public async Task GetReferencesAsync_ReturnsDeclarationAndUsages()
    {
        var service = new CsxLanguageService();
        const string content = """
            var pressure = 1;
            var copy = pressure + pressure;
            return copy;
            """;
        var position = content.IndexOf("pressure", StringComparison.Ordinal) + 1;

        var references = await service.GetReferencesAsync(
            "inline/tag-1.csx",
            content,
            position,
            includeDeclaration: true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Equal(3, references.Count(reference => reference.DocumentPath == "inline/tag-1.csx"));
    }

    [Fact]
    public async Task RenameAsync_ReturnsOnlyCurrentDocumentEdits()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common.csx"] = "int SharedValue() => 42;",
        };
        const string content = """
            #load "common.csx"
            int LocalValue() => SharedValue();
            return LocalValue();
            """;
        var position = content.LastIndexOf("LocalValue", StringComparison.Ordinal) + 1;

        var edit = await service.RenameAsync(
            "machine.csx",
            content,
            position,
            "RenamedValue",
            visibleScripts,
            typeof(TagScriptGlobals));

        Assert.NotNull(edit);
        var documentEdit = Assert.Single(edit.DocumentEdits);
        Assert.Equal("machine.csx", documentEdit.DocumentPath);
        Assert.Equal(2, documentEdit.Edits.Count);
        Assert.All(documentEdit.Edits, textEdit => Assert.Equal("RenamedValue", textEdit.NewText));
    }

    [Fact]
    public async Task FormatDocumentAsync_ReturnsWholeDocumentEdit()
    {
        var service = new CsxLanguageService();

        var edits = await service.FormatDocumentAsync(
            "inline/tag-1.csx",
            "if(true){return 1;}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        var edit = Assert.Single(edits);
        Assert.Equal("if (true) { return 1; }", edit.NewText);
    }

    [Fact]
    public async Task FormatDocumentAsync_PreservesBlankLineAfterLoadDirectives()
    {
        var service = new CsxLanguageService();
        const string content = """
            #load "utils.csx"

            public int Test(int qweqe){return Clamp(123,123,qweqe);}
            """;

        var edits = await service.FormatDocumentAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        var edit = Assert.Single(edits);
        Assert.Contains($"#load \"utils.csx\"{Environment.NewLine}{Environment.NewLine}public int Test", edit.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormatDocumentAsync_PreservesMultipleBlankLinesBetweenClassMembers()
    {
        var service = new CsxLanguageService();
        const string content = """
            public class Example
            {
            public Example()
            {
            }


            private int value;
            }
            """;

        var edits = await service.FormatDocumentAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        var edit = Assert.Single(edits);
        Assert.Contains($"    }}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}    private int value;", edit.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFoldingRangesAsync_ReturnsBlockRanges()
    {
        var service = new CsxLanguageService();
        const string content = """
            if (true)
            {
                return 1;
            }
            return 0;
            """;

        var ranges = await service.GetFoldingRangesAsync(
            "inline/tag-1.csx",
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.Contains(ranges, range => range.StartLine == 1 && range.EndLine == 3);
    }

    [Fact]
    public async Task GetSemanticTokensAsync_ReturnsSemanticTokenData()
    {
        var service = new CsxLanguageService();

        var tokens = await service.GetSemanticTokensAsync(
            "inline/tag-1.csx",
            "var pressure = UniEmu.Tag.Name;",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        Assert.NotEmpty(tokens.Data);
        Assert.Contains("variable", tokens.Legend.TokenTypes);
        Assert.Contains("property", tokens.Legend.TokenTypes);
    }

    [Fact]
    public async Task PrepareCallHierarchyAsync_ReturnsCallableItem()
    {
        var service = new CsxLanguageService();
        const string content = """
            int Add(int left, int right) => left + right;
            int Twice() => Add(1, 2);
            return Twice();
            """;
        var position = content.IndexOf("Twice", StringComparison.Ordinal) + 1;

        var items = await service.PrepareCallHierarchyAsync(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            typeof(TagScriptGlobals));

        var item = Assert.Single(items);
        Assert.Equal("Twice", item.Name);
        Assert.Equal("inline/tag-1.csx", item.DocumentPath);
    }

    private static void AssertPrecedes(IReadOnlyList<CsxCompletionItem> completions, string firstLabel, string secondLabel)
    {
        var completionList = completions.ToList();
        var firstIndex = completionList.FindIndex(item => item.Label == firstLabel);
        var secondIndex = completionList.FindIndex(item => item.Label == secondLabel);

        Assert.True(firstIndex >= 0, $"Expected completion '{firstLabel}' to be present.");
        Assert.True(secondIndex >= 0, $"Expected completion '{secondLabel}' to be present.");
        Assert.True(firstIndex < secondIndex, $"Expected '{firstLabel}' to appear before '{secondLabel}'.");
    }

    private static void AssertSortTextPrecedes(IReadOnlyList<CsxCompletionItem> completions, string firstLabel, string secondLabel)
    {
        var first = Assert.Single(completions, item => item.Label == firstLabel);
        var second = Assert.Single(completions, item => item.Label == secondLabel);

        Assert.True(
            string.CompareOrdinal(first.SortText, second.SortText) < 0,
            $"Expected sortText for '{firstLabel}' ('{first.SortText}') to sort before '{secondLabel}' ('{second.SortText}').");
    }

    private static async Task<LanguageFeatureTiming> MeasureAsync<T>(string name, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = await action();
        stopwatch.Stop();
        return new LanguageFeatureTiming(name, stopwatch.Elapsed);
    }

    private sealed record LanguageFeatureTiming(string Name, TimeSpan Elapsed);
}
