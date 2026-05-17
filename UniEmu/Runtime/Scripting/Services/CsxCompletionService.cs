using Microsoft.CodeAnalysis.Completion;
using System.Reflection;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;
using UniEmu.Scripting.Api;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxCompletionService(CsxRoslynContextFactory contextFactory)
{
    private const string ScriptingApiNamespace = "UniEmu.Scripting.Api";
    private const string ScriptingApiNamespacePrefix = ScriptingApiNamespace + ".";

    private static readonly HashSet<string> s_allowedSystemTypeLabels = new(StringComparer.Ordinal)
    {
        "Convert",
        "DateTime",
        "DateTimeOffset",
        "Math",
        "StringComparer",
        "TimeSpan",
    };

    private static readonly HashSet<string> s_scriptSpecificCompletionTags = new(StringComparer.Ordinal)
    {
        "field",
        "local",
        "method",
        "parameter",
        "property",
    };

    private static readonly HashSet<string> s_additionalAllowedScriptCompletionTags = new(StringComparer.Ordinal)
    {
        "enummember",
        "keyword",
    };

    private static readonly Type[] s_scriptingApiTypes = typeof(ScriptingApiAttribute).Assembly.GetTypes();

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
            .Select(candidate => candidate.Item with
            {
                SortText = GetRankedSortText(candidate, globalObjectLabels),
            })
            .ToList();
    }

    private static bool IsAllowedCompletion(CompletionCandidate candidate)
    {
        var item = candidate.Item;
        if (IsScriptingApiCompletion(candidate))
        {
            return HasScriptingApiAttribute(candidate);
        }

        if (candidate.Tags.Any(IsAllowedScriptCompletionTag))
        {
            return true;
        }

        return s_allowedSystemTypeLabels.Contains(item.Label);
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

    private static string GetRankedSortText(CompletionCandidate candidate, IReadOnlySet<string> globalObjectLabels)
    {
        return $"{GetCompletionPriority(candidate, globalObjectLabels):D2}_{candidate.Item.SortText}_{candidate.Item.Label}";
    }

    private static bool IsScriptSpecificCompletion(CompletionCandidate candidate)
    {
        return candidate.Tags.Any(IsScriptSpecificCompletionTag)
            && !IsScriptingApiCompletion(candidate)
            && !IsSystemCompletion(candidate);
    }

    private static bool IsAllowedScriptCompletionTag(string tag)
    {
        return IsScriptSpecificCompletionTag(tag) || s_additionalAllowedScriptCompletionTags.Contains(tag);
    }

    private static bool IsScriptSpecificCompletionTag(string tag)
    {
        return s_scriptSpecificCompletionTags.Contains(tag);
    }

    private static bool IsScriptingApiCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains(ScriptingApiNamespacePrefix, StringComparison.Ordinal) == true;
    }

    private static bool IsSystemCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains("System.", StringComparison.Ordinal) == true;
    }

    private static bool HasScriptingApiAttribute(CompletionCandidate candidate)
    {
        if (candidate.Documentation is not { } documentation)
        {
            return false;
        }

        foreach (var type in s_scriptingApiTypes)
        {
            if (type.FullName is null || !documentation.Contains(type.FullName, StringComparison.Ordinal))
            {
                continue;
            }

            if (type.Name == candidate.Item.Label)
            {
                return type.IsDefined(typeof(ScriptingApiAttribute), inherit: false);
            }

            const BindingFlags memberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            if (type.GetMember(candidate.Item.Label, memberFlags)
                .Any(member => member.IsDefined(typeof(ScriptingApiAttribute), inherit: false)))
            {
                return true;
            }
        }

        return false;
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
