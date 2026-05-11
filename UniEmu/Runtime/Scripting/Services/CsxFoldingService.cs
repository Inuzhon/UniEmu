using Microsoft.CodeAnalysis.CSharp.Syntax;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxFoldingService(CsxRoslynContextFactory contextFactory)
{
    public async Task<IReadOnlyList<CsxFoldingRange>> GetFoldingRangesAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, 0, visibleScripts, globalsType);
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken);
        if (root is null)
        {
            return [];
        }

        return root.DescendantNodes()
            .Select(node => node switch
            {
                BlockSyntax block => block.GetLocation(),
                ClassDeclarationSyntax type => type.GetLocation(),
                MethodDeclarationSyntax method => method.GetLocation(),
                _ => null,
            })
            .OfType<Microsoft.CodeAnalysis.Location>()
            .Select(location => CsxSourceMapping.ToLocation(context, location))
            .OfType<CsxLocation>()
            .Where(location => location.DocumentPath == context.EntryPath)
            .Select(location => new CsxFoldingRange(
                location.Range.StartLine,
                location.Range.EndLine,
                "region"))
            .Where(range => range.EndLine > range.StartLine)
            .Distinct()
            .OrderBy(range => range.StartLine)
            .ToArray();
    }
}
