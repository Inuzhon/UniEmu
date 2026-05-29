using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxNavigationService(CsxRoslynContextFactory contextFactory)
{
    public async Task<IReadOnlyList<CsxLocation>> GetDefinitionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
        var symbol = await ResolveSymbolAsync(context, cancellationToken);

        return symbol is null
            ? []
            : SourceLocations(context, symbol.Locations);
    }

    public async Task<IReadOnlyList<CsxLocation>> GetTypeDefinitionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
        var symbol = await ResolveSymbolAsync(context, cancellationToken);
        var type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IMethodSymbol method => method.ReturnType,
            INamedTypeSymbol namedType => namedType,
            _ => null,
        };

        return type is null
            ? []
            : SourceLocations(context, type.Locations);
    }

    public async Task<IReadOnlyList<CsxLocation>> GetReferencesAsync(
        string entryPath,
        string content,
        int position,
        bool includeDeclaration,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
        var symbol = await ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null)
        {
            return [];
        }

        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            context.Document.Project.Solution,
            cancellationToken);

        var locations = new List<CsxLocation>();
        if (includeDeclaration)
        {
            locations.AddRange(SourceLocations(context, symbol.Locations));
        }

        foreach (var reference in references.SelectMany(reference => reference.Locations))
        {
            var location = CsxSourceMapping.ToLocation(context, reference.Location);
            if (location is not null)
            {
                locations.Add(location);
            }
        }

        return locations
            .Distinct()
            .OrderBy(location => location.DocumentPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.Range.StartLine)
            .ThenBy(location => location.Range.StartCharacter)
            .ToArray();
    }

    /// <summary>
    /// Возвращает имя и исходные объявления символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст входного документа.</param>
    /// <param name="position">Offset позиции в раскрытом документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Целевой символ для поиска ссылок или <see langword="null"/>.</returns>
    public async Task<CsxSymbolReferenceTarget?> GetReferenceTargetAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
        var symbol = await ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null || string.IsNullOrWhiteSpace(symbol.Name))
        {
            return null;
        }

        var declarations = SourceLocations(context, symbol.Locations);
        return declarations.Count == 0
            ? null
            : new CsxSymbolReferenceTarget(symbol.Name, declarations);
    }

    /// <summary>
    /// Возвращает ссылки во входном документе, которые Roslyn связывает с указанными объявлениями символа.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст входного документа.</param>
    /// <param name="symbolName">Имя искомого символа.</param>
    /// <param name="declarationLocations">Исходные расположения объявлений символа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список ссылок во входном документе.</returns>
    public async Task<IReadOnlyList<CsxLocation>> GetReferencesToTargetAsync(
        string entryPath,
        string content,
        string symbolName,
        IReadOnlyList<CsxLocation> declarationLocations,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        if (declarationLocations.Count == 0 || string.IsNullOrWhiteSpace(symbolName))
        {
            return [];
        }

        using var context = contextFactory.CreateContext(entryPath, content, 0, visibleScripts, globalsType, cancellationToken);
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return [];
        }

        var locations = new List<CsxLocation>();
        foreach (var token in root.DescendantTokens().Where(token =>
                     token.SpanStart >= context.EntryContentStart
                     && string.Equals(token.ValueText, symbolName, StringComparison.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = CsxRoslynSymbolHelpers.ResolveSymbol(semanticModel, token);
            if (symbol is null || !SymbolMatchesDeclarations(context, symbol, declarationLocations))
            {
                continue;
            }

            var location = CsxSourceMapping.ToLocation(context, token.GetLocation());
            if (location is not null)
            {
                locations.Add(location);
            }
        }

        return locations.Distinct().ToArray();
    }

    public async Task<IReadOnlyList<CsxLocation>> GetImplementationsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
        var symbol = await ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null)
        {
            return [];
        }

        var implementations = await SymbolFinder.FindImplementationsAsync(
            symbol,
            context.Document.Project.Solution,
            cancellationToken: cancellationToken);

        return implementations
            .SelectMany(symbol => SourceLocations(context, symbol.Locations))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Проверяет, что символ имеет хотя бы одно объявление из целевого набора.
    /// </summary>
    /// <param name="context">Roslyn-контекст текущего документа.</param>
    /// <param name="symbol">Проверяемый символ.</param>
    /// <param name="declarationLocations">Ожидаемые расположения объявлений.</param>
    /// <returns><see langword="true"/>, если символ соответствует целевому объявлению.</returns>
    private static bool SymbolMatchesDeclarations(
        CsxRoslynContext context,
        ISymbol symbol,
        IReadOnlyList<CsxLocation> declarationLocations)
    {
        var symbolDeclarations = SourceLocations(context, symbol.Locations);
        return symbolDeclarations.Any(declarationLocations.Contains);
    }

    internal static async Task<ISymbol?> ResolveSymbolAsync(CsxRoslynContext context, CancellationToken cancellationToken)
    {
        var document = context.Document;
        var sourceText = await document.GetTextAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        if (root is null || semanticModel is null || sourceText.Length == 0)
        {
            return null;
        }

        var token = root.FindToken(Math.Clamp(context.Position, 0, Math.Max(0, sourceText.Length - 1)));
        return CsxRoslynSymbolHelpers.ResolveSymbol(semanticModel, token);
    }

    private static IReadOnlyList<CsxLocation> SourceLocations(CsxRoslynContext context, IEnumerable<Location> locations)
    {
        return locations
            .Select(location => CsxSourceMapping.ToLocation(context, location))
            .OfType<CsxLocation>()
            .Distinct()
            .ToArray();
    }
}
