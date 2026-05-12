using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxSignatureHelpService(CsxRoslynContextFactory contextFactory)
{
    public async Task<CsxSignatureHelp?> GetSignatureHelpAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var document = context.Document;
        var sourceText = await document.GetTextAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        if (root is null || semanticModel is null)
        {
            return null;
        }

        var tokenPosition = Math.Clamp(context.Position - 1, 0, Math.Max(0, sourceText.Length - 1));
        var argumentList = root
            .FindToken(tokenPosition)
            .Parent?
            .AncestorsAndSelf()
            .OfType<BaseArgumentListSyntax>()
            .FirstOrDefault(argumentList => argumentList.Span.Start <= context.Position && context.Position <= argumentList.Span.End);

        if (argumentList is null)
        {
            return null;
        }

        var methods = CsxRoslynSymbolHelpers.ResolveCallableSymbols(semanticModel, argumentList)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>()
            .OrderBy(method => method.Parameters.Length)
            .ThenBy(method => method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();

        if (methods.Length == 0)
        {
            return null;
        }

        return new CsxSignatureHelp(
            methods.Select(method => new CsxSignature(
                    method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    CsxDocumentationFormatter.FromXml(method),
                    method.Parameters
                        .Select(parameter => new CsxSignatureParameter(
                            parameter.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            CsxDocumentationFormatter.FromXml(parameter)))
                        .ToArray()))
                .ToArray(),
            0,
            CsxRoslynSymbolHelpers.GetActiveParameter(argumentList, context.Position));
    }
}
