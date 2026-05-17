using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UniEmu.Runtime.Scripting.Common;

/// <summary>
/// Общие Roslyn-хелперы для CSX-языковых сервисов: разрешение символов,
/// перегрузок вызовов, активных аргументов и типов подсказок редактора.
/// </summary>
public static class CsxRoslynSymbolHelpers
{
    /// <summary>
    /// Сопоставляет Roslyn-теги подсказок с компактными типами, которые backend
    /// возвращает через REST API, а frontend затем переводит в <c>CompletionItemKind</c> Monaco.
    /// </summary>
    private static readonly Dictionary<string, string> s_completionKindByRoslynTag = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Class"] = "class",
        ["Constant"] = "constant",
        ["Delegate"] = "function",
        ["Enum"] = "enum",
        ["EnumMember"] = "enumMember",
        ["Event"] = "event",
        ["ExtensionMethod"] = "method",
        ["Field"] = "field",
        ["Interface"] = "interface",
        ["Keyword"] = "keyword",
        ["Label"] = "reference",
        ["Local"] = "variable",
        ["Method"] = "method",
        ["Module"] = "module",
        ["Namespace"] = "module",
        ["Operator"] = "operator",
        ["Parameter"] = "variable",
        ["Property"] = "property",
        ["RangeVariable"] = "variable",
        ["Struct"] = "struct",
        ["TypeParameter"] = "typeParameter",
    };

    /// <summary>
    /// Находит наиболее конкретный Roslyn-символ, соответствующий синтаксическому токену.
    /// </summary>
    /// <param name="semanticModel">Семантическая модель документа, которому принадлежит токен.</param>
    /// <param name="token">Токен в позиции курсора пользователя.</param>
    /// <returns>Найденный символ, символ-кандидат или <see langword="null"/>, если Roslyn не смог связать токен.</returns>
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

    /// <summary>
    /// Находит вызываемые символы для списка аргументов вызова метода или создания объекта.
    /// </summary>
    /// <param name="semanticModel">Семантическая модель текущего документа.</param>
    /// <param name="argumentList">Список аргументов под курсором.</param>
    /// <returns>Подходящие символы методов или конструкторов, включая кандидатов перегрузки.</returns>
    public static IEnumerable<ISymbol> ResolveCallableSymbols(SemanticModel semanticModel, BaseArgumentListSyntax argumentList)
    {
        return argumentList.Parent switch
        {
            InvocationExpressionSyntax invocation => ResolveInvocationSymbols(semanticModel, invocation),
            ObjectCreationExpressionSyntax creation => ResolveCreationSymbols(semanticModel, creation),
            _ => [],
        };
    }

    /// <summary>
    /// Вычисляет индекс активного аргумента внутри вызова метода или конструктора.
    /// </summary>
    /// <param name="argumentList">Список аргументов, которому принадлежит текущая позиция курсора.</param>
    /// <param name="position">Абсолютная позиция в раскрытом CSX-документе.</param>
    /// <returns>Индекс активного параметра, начиная с нуля.</returns>
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

    /// <summary>
    /// Преобразует Roslyn-теги подсказки в строковый тип, используемый REST API IntelliSense.
    /// </summary>
    /// <param name="tags">Roslyn-теги элемента автодополнения.</param>
    /// <returns>Тип подсказки backend-а или <c>text</c>, если тег неизвестен.</returns>
    public static string GetCompletionKind(IEnumerable<string> tags)
    {
        var tag = tags.FirstOrDefault();
        return tag is not null && s_completionKindByRoslynTag.TryGetValue(tag, out var kind)
            ? kind
            : "text";
    }

    /// <summary>
    /// Находит кандидатов перегрузки для выражения вызова метода.
    /// </summary>
    /// <param name="semanticModel">Семантическая модель текущего документа.</param>
    /// <param name="invocation">Выражение вызова, которому принадлежит список аргументов.</param>
    /// <returns>Связанные символы вызова или Roslyn-кандидаты, если разрешение перегрузки неполное.</returns>
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

    /// <summary>
    /// Находит кандидатов конструкторов для выражения создания объекта.
    /// </summary>
    /// <param name="semanticModel">Семантическая модель текущего документа.</param>
    /// <param name="creation">Выражение создания объекта, которому принадлежит список аргументов.</param>
    /// <returns>Связанные символы конструкторов или Roslyn-кандидаты, если разрешение перегрузки неполное.</returns>
    private static IEnumerable<ISymbol> ResolveCreationSymbols(SemanticModel semanticModel, ObjectCreationExpressionSyntax creation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(creation);
        return symbolInfo.Symbol is not null
            ? [symbolInfo.Symbol]
            : symbolInfo.CandidateSymbols;
    }
}
