using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UniEmu.Runtime.Scripting.Environment;

/// <summary>
/// Проверяет скомпилированный CSX-скрипт на использование API, которые недоступны пользовательскому коду.
/// </summary>
public sealed class CsxScriptSecurityValidator
{
    /// <summary>
    /// Префиксы типов и пространств имен, доступ к которым запрещен из пользовательских скриптов.
    /// </summary>
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

    /// <summary>
    /// Анализирует все syntax tree компиляции и возвращает диагностики нарушений безопасности.
    /// </summary>
    /// <param name="compilation">Компиляция Roslyn, созданная для пользовательского скрипта.</param>
    /// <returns>Список ошибок безопасности, найденных в коде скрипта.</returns>
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

    /// <summary>
    /// Проверяет, принадлежит ли символ типа запрещенному пространству имен или системному типу.
    /// </summary>
    /// <param name="typeSymbol">Символ типа, полученный из semantic model.</param>
    /// <returns><see langword="true"/>, если тип запрещен для пользовательского скрипта.</returns>
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

    /// <summary>
    /// Проверяет вызов по исходному синтаксису на случай, если semantic model не смог разрешить символ.
    /// </summary>
    /// <param name="invocation">Синтаксический узел вызова метода или функции.</param>
    /// <returns><see langword="true"/>, если выражение вызова указывает на запрещенный API.</returns>
    private static bool IsForbiddenInvocationSyntax(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && IsForbiddenSyntax(memberAccess.Expression);
    }

    /// <summary>
    /// Проверяет текстовое имя синтаксического узла на совпадение с запрещенными префиксами.
    /// </summary>
    /// <param name="syntax">Синтаксический узел, содержащий имя типа или выражения.</param>
    /// <returns><see langword="true"/>, если имя указывает на запрещенный API.</returns>
    private static bool IsForbiddenSyntax(SyntaxNode syntax)
    {
        var name = syntax.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
        return s_forbiddenTypePrefixes.Any(prefix =>
            name.Equals(prefix.TrimEnd('.'), StringComparison.Ordinal) ||
            name.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Создает диагностическое сообщение безопасности по позиции синтаксического узла.
    /// </summary>
    /// <param name="code">Код диагностики безопасности.</param>
    /// <param name="message">Текст сообщения для пользователя.</param>
    /// <param name="node">Узел, на котором обнаружено нарушение.</param>
    /// <param name="syntaxTree">Syntax tree, которому принадлежит узел.</param>
    /// <returns>Диагностика CSX с координатами проблемного участка.</returns>
    private static CsxDiagnostic CreateIssue(string code, string message, SyntaxNode node, SyntaxTree syntaxTree)
    {
        var span = syntaxTree.GetMappedLineSpan(node.Span);
        return new CsxDiagnostic(
            code,
            message,
            CsxDiagnosticSeverity.Error,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character,
            string.IsNullOrWhiteSpace(span.Path) ? null : TagScriptPath.Normalize(span.Path));
    }
}
