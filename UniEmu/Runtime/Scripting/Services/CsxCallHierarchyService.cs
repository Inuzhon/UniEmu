using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxCallHierarchyService(CsxRoslynContextFactory contextFactory)
{
    public async Task<IReadOnlyList<CsxCallHierarchyItem>> PrepareAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var symbol = await CsxNavigationService.ResolveSymbolAsync(context, cancellationToken);
        var item = symbol is null ? null : ToItem(context, symbol);
        return item is null ? [] : [item];
    }

    public async Task<IReadOnlyList<CsxCallHierarchyIncomingCall>> GetIncomingCallsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var symbol = await CsxNavigationService.ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null)
        {
            return [];
        }

        var callers = await SymbolFinder.FindCallersAsync(symbol, context.Document.Project.Solution, cancellationToken);
        return callers
            .Select(caller => ToIncomingCall(context, caller))
            .OfType<CsxCallHierarchyIncomingCall>()
            .ToArray();
    }

    public async Task<IReadOnlyList<CsxCallHierarchyOutgoingCall>> GetOutgoingCallsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var symbol = await CsxNavigationService.ResolveSymbolAsync(context, cancellationToken);
        if (symbol is null)
        {
            return [];
        }

        var root = await context.Document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);
        var declaration = symbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .FirstOrDefault();

        if (root is null || semanticModel is null || declaration is null)
        {
            return [];
        }

        return declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol)
            .OfType<ISymbol>()
            .Select(called => ToOutgoingCall(context, called))
            .OfType<CsxCallHierarchyOutgoingCall>()
            .Distinct()
            .ToArray();
    }

    private static CsxCallHierarchyItem? ToItem(CsxRoslynContext context, ISymbol symbol)
    {
        var location = symbol.Locations
            .Select(location => CsxSourceMapping.ToLocation(context, location))
            .OfType<CsxLocation>()
            .FirstOrDefault();

        return location is null
            ? null
            : new CsxCallHierarchyItem(
                symbol.Name,
                symbol.Kind.ToString(),
                symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                location.DocumentPath,
                location.Range,
                location.Range);
    }

    private static CsxCallHierarchyIncomingCall? ToIncomingCall(CsxRoslynContext context, SymbolCallerInfo caller)
    {
        var from = ToItem(context, caller.CallingSymbol);
        if (from is null)
        {
            return null;
        }

        return new CsxCallHierarchyIncomingCall(
            from,
            caller.Locations
                .Select(location => CsxSourceMapping.ToLocation(context, location))
                .OfType<CsxLocation>()
                .Select(location => location.Range)
                .ToArray());
    }

    private static CsxCallHierarchyOutgoingCall? ToOutgoingCall(CsxRoslynContext context, ISymbol called)
    {
        var to = ToItem(context, called);
        return to is null ? null : new CsxCallHierarchyOutgoingCall(to, []);
    }
}
