using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Runtime;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxIntellisenseService(
    UniEmuDbContext db,
    CsxLanguageService language)
{
    public async Task<IReadOnlyList<CsxDiagnostic>> GetDiagnosticsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return language.Analyze(
                EntryPath(context),
                sourceCode,
                visibleScripts,
                typeof(TagScriptGlobals))
            .Diagnostics;
    }

    public async Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return language.GetCompletions(
            EntryPath(context),
            sourceCode,
            ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals));
    }

    public async Task<CsxHover?> GetHoverAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return language.GetHover(
            EntryPath(context),
            sourceCode,
            ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals));
    }

    public async Task<CsxSignatureHelp?> GetSignatureHelpAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return language.GetSignatureHelp(
            EntryPath(context),
            sourceCode,
            ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals));
    }

    private async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(
        CsxDocumentContext context,
        string sourceCode,
        CancellationToken cancellationToken)
    {
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
        var query = db.ScriptFiles
            .AsNoTracking()
            .Where(script => script.Scope == sharedScope || script.EmulatorId == context.EmulatorId);

        var scripts = await query
            .OrderBy(script => script.Scope == sharedScope ? 0 : 1)
            .ThenBy(script => script.Name)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts)
        {
            result[TagScriptPath.Normalize(script.Name)] = script.Content;
        }

        if (!string.IsNullOrWhiteSpace(context.ScriptName))
        {
            result[TagScriptPath.Normalize(context.ScriptName)] = sourceCode;
        }

        return result;
    }

    private static string EntryPath(CsxDocumentContext context)
    {
        return TagScriptPath.Normalize(string.IsNullOrWhiteSpace(context.ScriptName)
            ? "script.csx"
            : context.ScriptName);
    }

    private static int ToOffset(string sourceCode, CsxEditorPosition? position)
    {
        if (position is null)
        {
            return sourceCode.Length;
        }

        var targetLine = Math.Max(0, position.Line - 1);
        var targetColumn = Math.Max(0, position.Column - 1);
        var currentLine = 0;
        var offset = 0;
        while (currentLine < targetLine && offset < sourceCode.Length)
        {
            var next = sourceCode.IndexOf('\n', offset);
            if (next < 0)
            {
                return sourceCode.Length;
            }

            offset = next + 1;
            currentLine++;
        }

        return Math.Clamp(offset + targetColumn, 0, sourceCode.Length);
    }
}

public sealed record CsxIntellisenseRequest(
    string? SourceCode,
    string? DocumentUri,
    CsxEditorPosition? Position);

public sealed record CsxEditorPosition(int Line, int Column);
