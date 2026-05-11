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
    public async Task AnalyzeAsync_ReturnsCompilerErrorDiagnostic_WhenScriptReturnBranchesHaveIncompatibleTypes()
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
            typeof(TagScriptGlobals));

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

    private static void AssertPrecedes(IReadOnlyList<CsxCompletionItem> completions, string firstLabel, string secondLabel)
    {
        var completionList = completions.ToList();
        var firstIndex = completionList.FindIndex(item => item.Label == firstLabel);
        var secondIndex = completionList.FindIndex(item => item.Label == secondLabel);

        Assert.True(firstIndex >= 0, $"Expected completion '{firstLabel}' to be present.");
        Assert.True(secondIndex >= 0, $"Expected completion '{secondLabel}' to be present.");
        Assert.True(firstIndex < secondIndex, $"Expected '{firstLabel}' to appear before '{secondLabel}'.");
    }
}
