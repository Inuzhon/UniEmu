using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptSecurityValidator
{
    private static readonly string[] s_forbiddenTypePrefixes =
    [
        "System.IO.",
        "System.Net.",
        "System.Reflection.",
        "System.Diagnostics.",
        "System.Threading.",
        "System.Runtime.InteropServices.",
        "System.Security.",
        "System.Environment",
        "System.AppDomain",
        "System.Activator",
        "System.Type",
        "System.GC",
        "System.Console",
    ];

    public IReadOnlyList<CsxDiagnostic> Validate(Compilation compilation)
    {
        var issues = new List<CsxDiagnostic>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var node in root.DescendantNodes())
            {
                if (node.IsKind(SyntaxKind.UnsafeStatement) || node.IsKind(SyntaxKind.PointerType))
                {
                    issues.Add(CreateIssue("SEC001", "Unsafe code is not allowed.", node, syntaxTree));
                    continue;
                }

                if (node is ObjectCreationExpressionSyntax objectCreation)
                {
                    var createdType = semanticModel.GetTypeInfo(objectCreation).Type;
                    if (IsForbidden(createdType) || IsForbiddenSyntax(objectCreation.Type))
                    {
                        issues.Add(CreateIssue("SEC002", "Using this type is not allowed in user scripts.", objectCreation, syntaxTree));
                    }
                }

                if (node is InvocationExpressionSyntax invocation)
                {
                    var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (IsForbidden(methodSymbol?.ContainingType) || IsForbiddenInvocationSyntax(invocation))
                    {
                        issues.Add(CreateIssue("SEC003", "Calling this API is not allowed in user scripts.", invocation, syntaxTree));
                    }
                }

                if (node is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (symbol is not null && IsForbidden(symbol.ContainingType))
                    {
                        issues.Add(CreateIssue("SEC004", "Access to this member is not allowed in user scripts.", memberAccess, syntaxTree));
                    }
                }
            }
        }

        return issues;
    }

    private static bool IsForbidden(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

        return s_forbiddenTypePrefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsForbiddenInvocationSyntax(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && IsForbiddenSyntax(memberAccess.Expression);
    }

    private static bool IsForbiddenSyntax(SyntaxNode syntax)
    {
        var name = syntax.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
        return s_forbiddenTypePrefixes.Any(prefix =>
            name.Equals(prefix.TrimEnd('.'), StringComparison.Ordinal) ||
            name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static CsxDiagnostic CreateIssue(string code, string message, SyntaxNode node, SyntaxTree syntaxTree)
    {
        var span = syntaxTree.GetLineSpan(node.Span);
        return new CsxDiagnostic(
            code,
            message,
            CsxDiagnosticSeverity.Error,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character);
    }
}
