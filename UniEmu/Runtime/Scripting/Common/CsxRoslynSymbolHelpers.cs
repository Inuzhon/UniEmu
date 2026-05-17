using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UniEmu.Runtime.Scripting.Common;

public static class CsxRoslynSymbolHelpers
{
    public static ISymbol? ResolveSymbol(SemanticModel semanticModel, SyntaxToken token)
    {
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? [])
        {
            var symbol = semanticModel.GetSymbolInfo(node).Symbol
                         ?? semanticModel.GetDeclaredSymbol(node)
                         ?? semanticModel.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
            if (symbol is not null)
            {
                return symbol;
            }
        }

        return null;
    }

    public static IEnumerable<ISymbol> ResolveCallableSymbols(SemanticModel semanticModel, BaseArgumentListSyntax argumentList)
    {
        return argumentList.Parent switch
        {
            InvocationExpressionSyntax invocation => ResolveInvocationSymbols(semanticModel, invocation),
            ObjectCreationExpressionSyntax creation => ResolveCreationSymbols(semanticModel, creation),
            _ => [],
        };
    }

    public static int GetActiveParameter(BaseArgumentListSyntax argumentList, int position)
    {
        var activeParameter = 0;

        foreach (var argument in argumentList.Arguments)
        {
            if (argument.Span.End < position)
            {
                activeParameter++;
            }
        }

        return activeParameter;
    }

    public static string GetCompletionKind(IEnumerable<string> tags)
    {
        var tag = tags.FirstOrDefault()?.ToLowerInvariant();
        return tag switch
        {
            "method" => "method",
            "property" => "property",
            "class" => "class",
            "struct" => "struct",
            "enum" => "enum",
            "enummember" => "enumMember",
            "field" => "field",
            "keyword" => "keyword",
            "local" => "variable",
            "parameter" => "variable",
            _ => "text",
        };
    }

    private static IEnumerable<ISymbol> ResolveInvocationSymbols(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var group = semanticModel.GetMemberGroup(invocation.Expression);
        if (group.Length > 0)
        {
            return group;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        return symbolInfo.Symbol is not null
            ? [symbolInfo.Symbol]
            : symbolInfo.CandidateSymbols;
    }

    private static IEnumerable<ISymbol> ResolveCreationSymbols(SemanticModel semanticModel, ObjectCreationExpressionSyntax creation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(creation);
        return symbolInfo.Symbol is not null
            ? [symbolInfo.Symbol]
            : symbolInfo.CandidateSymbols;
    }
}
