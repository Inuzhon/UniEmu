using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Runtime.Scripting.Environment;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxDiagnosticsService
{
    private readonly CsxScriptEnvironment environment;
    private readonly CsxScriptSecurityValidator securityValidator;
    private readonly CsxScriptDirectiveValidator directiveValidator;

    public CsxDiagnosticsService(CsxScriptEnvironment environment)
        : this(environment, new CsxScriptSecurityValidator(), new CsxScriptDirectiveValidator())
    {
    }

    public CsxDiagnosticsService(
        CsxScriptEnvironment environment,
        CsxScriptSecurityValidator securityValidator,
        CsxScriptDirectiveValidator directiveValidator)
    {
        this.environment = environment;
        this.securityValidator = securityValidator;
        this.directiveValidator = directiveValidator;
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

        var directiveDiagnostics = directiveValidator.GetUnsupportedDirectiveDiagnostics(entryPath, content, visibleScripts);
        if (directiveDiagnostics.Count > 0)
        {
            return Task.FromResult(directiveDiagnostics);
        }

        var options = environment.CreateScriptOptions(entryPath, visibleScripts, globalsType);
        var script = CreateScript(CsxNullableContext.Apply(content, entryPath), options, globalsType, expectedReturnType);

        var compilerDiagnostics = script.Compile(cancellationToken)
            .Select(ToCsxDiagnostic)
            .ToList();
        var securityDiagnostics = securityValidator.Validate(script.GetCompilation());

        IReadOnlyList<CsxDiagnostic> diagnostics = compilerDiagnostics
            .Concat(securityDiagnostics)
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
        if (!diagnostic.Location.IsInSource)
        {
            return new CsxDiagnostic(
                diagnostic.Id,
                diagnostic.GetMessage(),
                ToSeverity(diagnostic.Severity),
                0,
                0,
                0,
                0);
        }

        var span = diagnostic.Location.GetMappedLineSpan();
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;
        return new CsxDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            ToSeverity(diagnostic.Severity),
            start.Line,
            start.Character,
            end.Line,
            end.Character,
            string.IsNullOrWhiteSpace(span.Path) ? null : TagScriptPath.Normalize(span.Path));
    }

    private static CsxDiagnosticSeverity ToSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => CsxDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => CsxDiagnosticSeverity.Warning,
            DiagnosticSeverity.Info => CsxDiagnosticSeverity.Information,
            _ => CsxDiagnosticSeverity.Hint,
        };
    }
}
