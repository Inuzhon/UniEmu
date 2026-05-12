using UniEmu.Scripting.Api;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxLanguageServiceTests
{
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
}
