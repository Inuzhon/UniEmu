using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.FindSymbols;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxRenameService(CsxRoslynContextFactory contextFactory)
{
    private static readonly Regex s_identifier = new(
        @"^[_\p{L}][_\p{L}\p{Nd}]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<CsxWorkspaceEdit?> RenameAsync(
        string entryPath,
        string content,
        int position,
        string newName,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        if (!s_identifier.IsMatch(newName))
        {
            return null;
        }

        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var symbol = await CsxNavigationService.ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null)
        {
            return null;
        }

        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            context.Document.Project.Solution,
            cancellationToken);

        var edits = symbol.Locations
            .Concat(references.SelectMany(reference => reference.Locations).Select(reference => reference.Location))
            .Where(location => CsxSourceMapping.IsEntryLocation(context, location))
            .Select(location => new CsxTextEdit(CsxSourceMapping.ToLocation(context, location)!.Range, newName))
            .Distinct()
            .OrderBy(edit => edit.Range.StartLine)
            .ThenBy(edit => edit.Range.StartCharacter)
            .ToArray();

        return edits.Length == 0
            ? null
            : new CsxWorkspaceEdit([new CsxDocumentEdit(context.EntryPath, edits)]);
    }
}
