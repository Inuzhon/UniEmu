using UniEmu.Scripting.Api;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxLanguageServiceTests
{
    [Fact]
    public void Analyze_ReturnsCompilerErrorDiagnostic_WhenScriptReferencesUnknownIdentifier()
    {
        var service = new CsxLanguageService();

        var result = service.Analyze(
            "inline/tag-1.csx",
            "return MissingValue;",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CS0103");
    }

    [Fact]
    public void Analyze_UsesLoadedScriptContent_WhenEntryScriptHasLoadDirective()
    {
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags/shared/math.csx"] = "int Add(int a, int b) => a + b;",
        };

        var result = service.Analyze(
            "tags/tag-1.csx",
            "#load \"shared/math.csx\"\nreturn Add(1, 2);",
            visibleScripts);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

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
        var displays = references
            .Select(reference => Path.GetFileName(reference.Display ?? string.Empty))
            .ToList();

        Assert.Contains("UniEmu.Scripting.Api.dll", displays);
        Assert.DoesNotContain("UniEmu.dll", displays);
    }

    [Fact]
    public void GetCompletions_ReturnsSymbolsFromLoadedScripts()
    {
        CsxLanguageService.ClearMetadataReferenceCacheForTests();
        var service = new CsxLanguageService();
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math.csx"] = "double LoadedHelper(double value) => value * 2;",
        };
        const string content = "#load \"math.csx\"\nreturn Load";

        var completions = service.GetCompletions(
            "inline/tag-1.csx",
            content,
            content.Length,
            visibleScripts);

        Assert.Contains(completions, item => item.Label == "LoadedHelper");
        var cacheCount = CsxLanguageService.MetadataReferenceCacheCount;
        Assert.True(cacheCount >= 1);

        _ = service.GetCompletions(
            "inline/tag-1.csx",
            content,
            content.Length,
            visibleScripts);

        Assert.Equal(cacheCount, CsxLanguageService.MetadataReferenceCacheCount);
    }

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
    public void GetHover_ReturnsSymbolSignature()
    {
        var service = new CsxLanguageService();
        const string content = "var value = Math.Round(1.2);";
        var position = content.IndexOf("Round", StringComparison.Ordinal) + 2;

        var hover = service.GetHover(
            "inline/tag-1.csx",
            content,
            position,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(hover);
        Assert.Contains("Round", hover.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void GetHover_ReturnsScriptingApiSymbolForUniEmuTags()
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

    [Fact]
    public void GetSignatureHelp_ReturnsMethodParameters()
    {
        var service = new CsxLanguageService();
        const string content = "var value = Math.Round(";

        var signatureHelp = service.GetSignatureHelp(
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
}
