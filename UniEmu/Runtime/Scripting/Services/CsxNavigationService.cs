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
