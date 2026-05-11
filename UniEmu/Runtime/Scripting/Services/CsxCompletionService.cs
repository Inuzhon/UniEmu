using Microsoft.CodeAnalysis.Completion;
using System.Reflection;
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

        var globalObjectLabels = GetGlobalObjectLabels(globalsType);

        return candidates
            .Where(IsAllowedCompletion)
            .DistinctBy(candidate => candidate.Item.Label, StringComparer.Ordinal)
            .OrderBy(candidate => GetCompletionPriority(candidate, globalObjectLabels))
            .ThenBy(candidate => candidate.Item.SortText, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Item.Label, StringComparer.Ordinal)
            .Select(candidate => candidate.Item)
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

    private static int GetCompletionPriority(CompletionCandidate candidate, IReadOnlySet<string> globalObjectLabels)
    {
        if (globalObjectLabels.Contains(candidate.Item.Label))
        {
            return 0;
        }

        if (IsScriptSpecificCompletion(candidate))
        {
            return 1;
        }

        if (IsScriptingApiCompletion(candidate))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsScriptSpecificCompletion(CompletionCandidate candidate)
    {
        return candidate.Tags.Any(tag => tag is "method" or "property" or "field" or "local" or "parameter")
            && !IsScriptingApiCompletion(candidate)
            && !IsSystemCompletion(candidate);
    }

    private static bool IsScriptingApiCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains("UniEmu.Scripting.Api.", StringComparison.Ordinal) == true;
    }

    private static bool IsSystemCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains("System.", StringComparison.Ordinal) == true;
    }

    private static IReadOnlySet<string> GetGlobalObjectLabels(Type globalsType)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        AddGlobalObjectLabels(globalsType, labels, []);
        return labels;
    }

    private static void AddGlobalObjectLabels(Type type, HashSet<string> labels, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            labels.Add(property.Name);
            if (property.PropertyType.Namespace == "UniEmu.Scripting.Api")
            {
                AddGlobalObjectLabels(property.PropertyType, labels, visited);
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (!method.IsSpecialName)
            {
                labels.Add(method.Name);
            }
        }
    }

    private sealed record CompletionCandidate(
        IReadOnlyList<string> Tags,
        string? Documentation,
        CsxCompletionItem Item);
}
