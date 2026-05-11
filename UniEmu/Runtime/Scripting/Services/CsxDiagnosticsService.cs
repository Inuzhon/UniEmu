using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Runtime.Scripting.Environment;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxDiagnosticsService
{
    private readonly CsxScriptEnvironment environment;

    public CsxDiagnosticsService(CsxScriptEnvironment environment)
    {
        this.environment = environment;
    }

    public Task<IReadOnlyList<CsxDiagnostic>> AnalyzeAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        Type? expectedReturnType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedContent = TagScriptContentNormalizer.NormalizeEntryScriptContent(content);
        var options = environment.CreateScriptOptions(entryPath, visibleScripts);
        var script = CreateScript(normalizedContent, options, globalsType, expectedReturnType);

        IReadOnlyList<CsxDiagnostic> diagnostics = script.Compile(cancellationToken)
            .Select(ToCsxDiagnostic)
            .ToList();

        return Task.FromResult(diagnostics);
    }

    private static Script CreateScript(
        string content,
        ScriptOptions options,
        Type globalsType,
        Type? expectedReturnType)
    {
        if (expectedReturnType == typeof(bool))
            return CSharpScript.Create<bool>(content, options, globalsType);

        if (expectedReturnType == typeof(int))
            return CSharpScript.Create<int>(content, options, globalsType);

        if (expectedReturnType == typeof(double))
            return CSharpScript.Create<double>(content, options, globalsType);

        if (expectedReturnType == typeof(string))
            return CSharpScript.Create<string>(content, options, globalsType);

        return CSharpScript.Create<object?>(content, options, globalsType);
    }

    private static CsxDiagnostic ToCsxDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;
        return new CsxDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => CsxDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => CsxDiagnosticSeverity.Warning,
                DiagnosticSeverity.Info => CsxDiagnosticSeverity.Information,
                _ => CsxDiagnosticSeverity.Hint,
            },
            start.Line,
            start.Character,
            end.Line,
            end.Character);
    }
}
