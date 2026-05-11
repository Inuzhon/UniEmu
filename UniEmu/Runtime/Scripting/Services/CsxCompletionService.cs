using Microsoft.CodeAnalysis.Completion;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxCompletionService(CsxRoslynContextFactory contextFactory)
{
    private static readonly HashSet<string> s_allowedSystemTypeLabels = new(StringComparer.Ordinal)
    {
        "Convert",
        "DateTime",
        "DateTimeOffset",
        "Math",
        "StringComparer",
        "TimeSpan",
    };

    public async Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType);
        var document = context.Document;
        var service = RoslynCompletionService.GetService(document);
        if (service is null)
        {
            return [];
        }

        var completionList = await service.GetCompletionsAsync(document, context.Position, cancellationToken: cancellationToken);

        if (completionList is null)
        {
            return [];
        }

        var candidates = new List<CompletionCandidate>(completionList.ItemsList.Count);
        foreach (var item in completionList.ItemsList)
        {
            var description = await service.GetDescriptionAsync(document, item, cancellationToken);
            var documentation = description is null ? null : CsxDocumentationFormatter.FromTaggedParts(description.TaggedParts);
            candidates.Add(new CompletionCandidate(
                item.Tags.Select(tag => tag.ToLowerInvariant()).ToArray(),
                documentation,
                new CsxCompletionItem(
                    item.DisplayText,
                    item.SortText,
                    item.FilterText,
                    item.Properties.TryGetValue("SymbolName", out var symbolName) ? symbolName : item.DisplayText,
                    item.InlineDescription,
                    documentation,
                    CsxRoslynSymbolHelpers.GetCompletionKind(item.Tags))));
        }

        return candidates
            .Where(IsAllowedCompletion)
            .Select(candidate => candidate.Item)
            .DistinctBy(item => item.Label, StringComparer.Ordinal)
            .OrderBy(item => item.SortText, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsAllowedCompletion(CompletionCandidate candidate)
    {
        var item = candidate.Item;
        if (candidate.Tags.Any(tag => tag is "keyword" or "method" or "property" or "field" or "local" or "parameter"))
        {
            return true;
        }

        if (s_allowedSystemTypeLabels.Contains(item.Label))
        {
            return true;
        }

        return candidate.Documentation?.Contains("UniEmu.Scripting.Api.", StringComparison.Ordinal) == true;
    }

    private sealed record CompletionCandidate(
        IReadOnlyList<string> Tags,
        string? Documentation,
        CsxCompletionItem Item);
}
