using Microsoft.CodeAnalysis;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxDiagnosticsService(CsxRoslynContextFactory contextFactory)
{
    public IReadOnlyList<CsxDiagnostic> Analyze(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        using var context = contextFactory.CreateContext(entryPath, content, 0, visibleScripts, globalsType);
        var compilation = context.Document.Project.GetCompilationAsync().GetAwaiter().GetResult();
        if (compilation is null)
        {
            return [];
        }

        return compilation.GetDiagnostics()
            .Select(ToCsxDiagnostic)
            .ToList();
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
