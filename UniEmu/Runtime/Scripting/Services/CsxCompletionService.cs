using Microsoft.CodeAnalysis.Completion;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxCompletionService(CsxRoslynContextFactory contextFactory)
{
    public IReadOnlyList<CsxCompletionItem> GetCompletions(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var document = context.Document;
        var service = RoslynCompletionService.GetService(document);
        if (service is null)
        {
            return [];
        }

        var completionList = service
            .GetCompletionsAsync(document, context.Position)
            .GetAwaiter()
            .GetResult();

        if (completionList is null)
        {
            return [];
        }

        return completionList.ItemsList
            .Select(item =>
            {
                var description = service.GetDescriptionAsync(document, item).GetAwaiter().GetResult();
                return new CsxCompletionItem(
                    item.DisplayText,
                    item.SortText,
                    item.FilterText,
                    item.Properties.TryGetValue("SymbolName", out var symbolName) ? symbolName : item.DisplayText,
                    item.InlineDescription,
                    description is null ? null : CsxDocumentationFormatter.FromTaggedParts(description.TaggedParts),
                    CsxRoslynSymbolHelpers.GetCompletionKind(item.Tags));
            })
            .DistinctBy(item => item.Label, StringComparer.Ordinal)
            .OrderBy(item => item.SortText, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToList();
    }
}
