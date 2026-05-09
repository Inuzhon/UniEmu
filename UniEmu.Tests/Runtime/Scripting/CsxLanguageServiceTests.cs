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
    public void GetCompletions_ReturnsSymbolsFromLoadedScripts()
    {
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
    }
}
