using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Services;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxLanguageService
{
    private static readonly CsxScriptEnvironment s_defaultEnvironment = new();

    private readonly CsxDiagnosticsService diagnostics;
    private readonly CsxCompletionService completion;
    private readonly CsxHoverService hover;
    private readonly CsxSignatureHelpService signatureHelp;

    public CsxLanguageService()
        : this(CreateDefaultContextFactory())
    {
    }

    private CsxLanguageService(CsxRoslynContextFactory contextFactory)
        : this(
            new CsxDiagnosticsService(s_defaultEnvironment),
            new CsxCompletionService(contextFactory),
            new CsxHoverService(contextFactory),
            new CsxSignatureHelpService(contextFactory))
    {
    }

    public CsxLanguageService(
        CsxDiagnosticsService diagnostics,
        CsxCompletionService completion,
        CsxHoverService hover,
        CsxSignatureHelpService signatureHelp)
    {
        this.diagnostics = diagnostics;
        this.completion = completion;
        this.hover = hover;
        this.signatureHelp = signatureHelp;
    }

    internal static int MetadataReferenceCacheCount => s_defaultEnvironment.MetadataReferenceCacheCount;

    internal static void ClearMetadataReferenceCacheForTests()
    {
        s_defaultEnvironment.ClearMetadataReferenceCacheForTests();
    }

    internal static IReadOnlyList<Microsoft.CodeAnalysis.MetadataReference> CreateMetadataReferencesForTests(Type globalsType)
    {
        return s_defaultEnvironment.CreateMetadataReferences(globalsType);
    }

    public async Task<CsxAnalysisResult> AnalyzeAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return await AnalyzeAsync(
            entryPath,
            content,
            visibleScripts,
            globalsType,
            expectedReturnType: null,
            cancellationToken);
    }

    public async Task<CsxAnalysisResult> AnalyzeAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType,
        Type? expectedReturnType,
        CancellationToken cancellationToken = default)
    {
        return new CsxAnalysisResult(
            entryPath,
            await diagnostics.AnalyzeAsync(
                TagScriptPath.Normalize(entryPath),
                content,
                visibleScripts,
                globalsType ?? typeof(object),
                expectedReturnType,
                cancellationToken));
    }

    public Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return completion.GetCompletionsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    public Task<CsxHover?> GetHoverAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return hover.GetHoverAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    public Task<CsxSignatureHelp?> GetSignatureHelpAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return signatureHelp.GetSignatureHelpAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    private static CsxRoslynContextFactory CreateDefaultContextFactory()
    {
        return new CsxRoslynContextFactory(s_defaultEnvironment, new CsxLoadedScriptExpander());
    }
}

public sealed record CsxAnalysisResult(
    string EntryPath,
    IReadOnlyList<CsxDiagnostic> Diagnostics);

public sealed record CsxDiagnostic(
    string Code,
    string Message,
    CsxDiagnosticSeverity Severity,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

public enum CsxDiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}

public sealed record CsxCompletionItem(
    string Label,
    string SortText,
    string FilterText,
    string InsertText,
    string? Detail = null,
    string? Documentation = null,
    string Kind = "text");

public sealed record CsxHover(
    string Signature,
    string? Documentation,
    int StartOffset,
    int EndOffset);

public sealed record CsxSignatureHelp(
    IReadOnlyList<CsxSignature> Signatures,
    int ActiveSignature,
    int ActiveParameter);

public sealed record CsxSignature(
    string Label,
    string? Documentation,
    IReadOnlyList<CsxSignatureParameter> Parameters);

public sealed record CsxSignatureParameter(
    string Label,
    string? Documentation);
