using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxSemanticTokensService(CsxRoslynContextFactory contextFactory)
{
    private static readonly CsxSemanticTokensLegend s_legend = new(
        ["class", "struct", "enum", "interface", "method", "property", "field", "variable", "parameter", "keyword", "number", "string", "comment"],
        []);

    public async Task<CsxSemanticTokens> GetSemanticTokensAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, 0, visibleScripts, globalsType, cancellationToken);
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return new CsxSemanticTokens(s_legend, []);
        }

        var tokens = root.DescendantTokens(descendIntoTrivia: true)
            .Select(token => ToSemanticToken(context, semanticModel, token))
            .OfType<SemanticToken>()
            .OrderBy(token => token.Line)
            .ThenBy(token => token.Character)
            .ToArray();

        return new CsxSemanticTokens(s_legend, Encode(tokens));
    }

    private static SemanticToken? ToSemanticToken(CsxRoslynContext context, SemanticModel semanticModel, SyntaxToken token)
    {
        if (token.Span.Start < context.EntryContentStart || token.Span.Length == 0)
        {
            return null;
        }

        var range = CsxSourceMapping.ToEntryRange(context, token.Span);
        var tokenType = GetTokenType(semanticModel, token);
        if (tokenType is null)
        {
            return null;
        }

        var tokenTypeIndex = s_legend.TokenTypes.ToList().IndexOf(tokenType);
        return tokenTypeIndex < 0
            ? null
            : new SemanticToken(range.StartLine, range.StartCharacter, token.Span.Length, tokenTypeIndex);
    }

    private static string? GetTokenType(SemanticModel semanticModel, SyntaxToken token)
    {
        if (token.IsKeyword())
        {
            return "keyword";
        }

        if (token.IsKind(SyntaxKind.NumericLiteralToken))
        {
            return "number";
        }

        if (token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return "string";
        }

        if (token.Parent is null)
        {
            return null;
        }

        var symbol = CsxRoslynSymbolHelpers.ResolveSymbol(semanticModel, token);
        return symbol switch
        {
            INamedTypeSymbol { TypeKind: TypeKind.Class } => "class",
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => "struct",
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => "enum",
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => "interface",
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IParameterSymbol => "parameter",
            ILocalSymbol => "variable",
            _ => null,
        };
    }

    private static int[] Encode(IReadOnlyList<SemanticToken> tokens)
    {
        var data = new List<int>(tokens.Count * 5);
        var previousLine = 0;
        var previousCharacter = 0;

        foreach (var token in tokens)
        {
            var deltaLine = token.Line - previousLine;
            var deltaStart = deltaLine == 0 ? token.Character - previousCharacter : token.Character;
            data.Add(deltaLine);
            data.Add(deltaStart);
            data.Add(token.Length);
            data.Add(token.TokenType);
            data.Add(0);

            previousLine = token.Line;
            previousCharacter = token.Character;
        }

        return data.ToArray();
    }

    private sealed record SemanticToken(int Line, int Character, int Length, int TokenType);
}
