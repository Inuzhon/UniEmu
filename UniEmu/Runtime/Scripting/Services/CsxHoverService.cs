using Microsoft.CodeAnalysis;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxHoverService(CsxRoslynContextFactory contextFactory)
{
    public CsxHover? GetHover(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var document = context.Document;
        var sourceText = document.GetTextAsync().GetAwaiter().GetResult();
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();

        if (root is null || semanticModel is null || sourceText.Length == 0)
        {
            return null;
        }

        var token = root.FindToken(Math.Clamp(context.Position, 0, Math.Max(0, sourceText.Length - 1)));
        var symbol = CsxRoslynSymbolHelpers.ResolveSymbol(semanticModel, token);
        if (symbol is null)
        {
            return null;
        }

        return new CsxHover(
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            CsxDocumentationFormatter.FromXml(symbol),
            token.SpanStart,
            token.Span.End);
    }
}
