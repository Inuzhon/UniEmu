using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Completion;
using System.Reflection;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Workspace;
using UniEmu.Scripting.Api;
using RoslynCompletionService = Microsoft.CodeAnalysis.Completion.CompletionService;

namespace UniEmu.Runtime.Scripting.Services;

/// <summary>
/// Строит подсказки автодополнения для CSX-скриптов через Roslyn и отфильтровывает
/// сырой список до символов, безопасных и полезных для скриптовой поверхности UniEmu.
/// </summary>
public sealed class CsxCompletionService(CsxRoslynContextFactory contextFactory)
{
    private const string ScriptingApiNamespace = "UniEmu.Scripting.Api";
    private const string ScriptingApiNamespacePrefix = ScriptingApiNamespace + ".";

    /// <summary>
    /// Небольшой allow-list системных типов, полезных в скриптах без открытия всех типов из подключенных сборок.
    /// </summary>
    private static readonly HashSet<string> s_allowedSystemTypeLabels = new(StringComparer.Ordinal)
    {
        "Convert",
        "DateTime",
        "DateTimeOffset",
        "Encoding",
        "Math",
        "StringBuilder",
        "StringComparer",
        "TimeSpan",
    };

    /// <summary>
    /// Roslyn-теги символов, объявленных текущим или загруженными скриптами.
    /// Такие подсказки разрешены и ранжируются выше публичного API и системных helper-типов.
    /// </summary>
    private static readonly HashSet<string> s_scriptSpecificCompletionTags = new(StringComparer.Ordinal)
    {
        "constant",
        "event",
        "field",
        "local",
        "method",
        "parameter",
        "property",
        "rangevariable",
        "typeparameter",
    };

    /// <summary>
    /// Дополнительные Roslyn-теги, полезные при редактировании скрипта, но не влияющие на script-specific ранжирование.
    /// </summary>
    private static readonly HashSet<string> s_additionalAllowedScriptCompletionTags = new(StringComparer.Ordinal)
    {
        "enummember",
        "keyword",
        "label",
        "operator",
    };

    /// <summary>
    /// Закэшированные reflection-метаданные скриптового API для проверки видимости через <see cref="ScriptingApiAttribute"/>.
    /// </summary>
    private static readonly Type[] s_scriptingApiTypes = typeof(ScriptingApiAttribute).Assembly.GetTypes();

    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> s_globalObjectLabels = new();

    /// <summary>
    /// Имена типов и членов скриптового API, для которых нужно оставить проверку документации,
    /// чтобы не показывать непомеченные публичные символы из сборки API.
    /// </summary>
    private static readonly HashSet<string> s_scriptingApiLabels = CreateScriptingApiLabels();

    /// <summary>
    /// Возвращает подсказки автодополнения для CSX-документа в указанной позиции исходного текста.
    /// </summary>
    /// <param name="entryPath">Путь или URI редактируемого входного CSX-документа.</param>
    /// <param name="content">Несохраненное содержимое документа из редактора.</param>
    /// <param name="position">Абсолютная позиция курсора в <paramref name="content"/>, начиная с нуля.</param>
    /// <param name="visibleScripts">Скрипты, доступные для разрешения директив <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта, задающий host API скрипта.</param>
    /// <param name="cancellationToken">Токен отмены для Roslyn-операций.</param>
    /// <returns>Отфильтрованные и отсортированные подсказки, готовые для REST-ответа IntelliSense.</returns>
    public async Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        using var context = contextFactory.CreateContext(entryPath, content, position, visibleScripts, globalsType, cancellationToken);
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
            cancellationToken.ThrowIfCancellationRequested();

            var tags = item.Tags.Select(tag => tag.ToLowerInvariant()).ToArray();
            var itemLabel = item.DisplayText;
            string? documentation = null;

            if (RequiresDescriptionForVisibility(itemLabel, tags))
            {
                var description = await service.GetDescriptionAsync(document, item, cancellationToken);
                documentation = description is null ? null : CsxDocumentationFormatter.FromTaggedParts(description.TaggedParts);
            }

            candidates.Add(new CompletionCandidate(
                tags,
                documentation,
                new CsxCompletionItem(
                    itemLabel,
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
            .Select(candidate => RankedCompletionCandidate.Create(candidate, globalObjectLabels))
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Item.SortText, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Item.Label, StringComparer.Ordinal)
            .Select(candidate => candidate.Item with
            {
                SortText = candidate.RankedSortText,
            })
            .ToList();
    }

    internal static bool RequiresDescriptionForVisibility(string label, IReadOnlyList<string> tags)
    {
        if (s_scriptingApiLabels.Contains(label))
        {
            return true;
        }

        if (s_allowedSystemTypeLabels.Contains(label))
        {
            return false;
        }

        return !tags.Any(IsAllowedScriptCompletionTag);
    }

    /// <summary>
    /// Определяет, виден ли Roslyn-кандидат автодополнения в пользовательских скриптах.
    /// </summary>
    /// <param name="candidate">Кандидат, полученный от Roslyn completion.</param>
    /// <returns><see langword="true"/>, если кандидат относится к публичному скриптовому API, script-local коду или allow-list системных helper-типов.</returns>
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

    /// <summary>
    /// Вычисляет базовый ранг подсказки, применяемый перед Roslyn sort text.
    /// </summary>
    /// <param name="candidate">Кандидат автодополнения, для которого рассчитывается ранг.</param>
    /// <param name="globalObjectLabels">Метки, напрямую доступные из графа globals-объекта.</param>
    /// <returns>Чем меньше число, тем выше подсказка должна появиться в Monaco.</returns>
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

    /// <summary>
    /// Добавляет ранг UniEmu к Roslyn sort text, чтобы Monaco сохранил серверный порядок подсказок.
    /// </summary>
    /// <param name="candidate">Кандидат автодополнения, для которого рассчитывается sort text.</param>
    /// <param name="priority">Уже рассчитанный ранг кандидата в текущем запросе completion.</param>
    /// <returns>Sort text, объединяющий приоритет UniEmu, порядок Roslyn и fallback по метке.</returns>
    private static string GetRankedSortText(CompletionCandidate candidate, int priority)
    {
        return $"{priority:D2}_{candidate.Item.SortText}_{candidate.Item.Label}";
    }

    /// <summary>
    /// Определяет подсказки из символов, объявленных скриптами, а не framework-символов или публичного API.
    /// </summary>
    /// <param name="candidate">Классифицируемый кандидат автодополнения.</param>
    /// <returns><see langword="true"/>, если кандидат должен ранжироваться как script-specific.</returns>
    private static bool IsScriptSpecificCompletion(CompletionCandidate candidate)
    {
        return candidate.Tags.Any(IsScriptSpecificCompletionTag)
            && !IsScriptingApiCompletion(candidate)
            && !IsSystemCompletion(candidate);
    }

    /// <summary>
    /// Проверяет, разрешен ли приведенный к нижнему регистру Roslyn-тег в скриптовых подсказках.
    /// </summary>
    /// <param name="tag">Roslyn-тег подсказки в нижнем регистре.</param>
    /// <returns><see langword="true"/>, если тег разрешен политикой скриптового автодополнения.</returns>
    private static bool IsAllowedScriptCompletionTag(string tag)
    {
        return IsScriptSpecificCompletionTag(tag) || s_additionalAllowedScriptCompletionTags.Contains(tag);
    }

    /// <summary>
    /// Проверяет, представляет ли приведенный к нижнему регистру Roslyn-тег script-specific символ.
    /// </summary>
    /// <param name="tag">Roslyn-тег подсказки в нижнем регистре.</param>
    /// <returns><see langword="true"/>, если тег участвует в script-specific ранжировании.</returns>
    private static bool IsScriptSpecificCompletionTag(string tag)
    {
        return s_scriptSpecificCompletionTags.Contains(tag);
    }

    /// <summary>
    /// Проверяет, указывает ли Roslyn-документация на пространство имен скриптового API UniEmu.
    /// </summary>
    /// <param name="candidate">Проверяемый кандидат автодополнения.</param>
    /// <returns><see langword="true"/> для кандидатов, разрешенных из <c>UniEmu.Scripting.Api</c>.</returns>
    private static bool IsScriptingApiCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains(ScriptingApiNamespacePrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Проверяет, указывает ли Roslyn-документация на framework- или BCL-символы.
    /// </summary>
    /// <param name="candidate">Проверяемый кандидат автодополнения.</param>
    /// <returns><see langword="true"/> для кандидатов, разрешенных из пространств имен <c>System.*</c>.</returns>
    private static bool IsSystemCompletion(CompletionCandidate candidate)
    {
        return candidate.Documentation?.Contains("System.", StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Проверяет, что кандидат из скриптового API явно помечен <see cref="ScriptingApiAttribute"/>.
    /// </summary>
    /// <param name="candidate">Кандидат автодополнения из сборки скриптового API.</param>
    /// <returns><see langword="true"/>, если соответствующий тип или член входит в публичную скриптовую поверхность.</returns>
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

    private static HashSet<string> CreateScriptingApiLabels()
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        const BindingFlags memberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in s_scriptingApiTypes)
        {
            labels.Add(type.Name);

            foreach (var member in type.GetMembers(memberFlags))
            {
                if (member is not ConstructorInfo)
                {
                    labels.Add(member.Name);
                }
            }
        }

        return labels;
    }

    /// <summary>
    /// Собирает метки, доступные из globals-типа и вложенных контекстных объектов скриптового API UniEmu.
    /// </summary>
    /// <param name="globalsType">Корневой globals-тип, используемый script host-ом.</param>
    /// <returns>Метки подсказок, которые должны получить максимальный приоритет.</returns>
    private static IReadOnlySet<string> GetGlobalObjectLabels(Type globalsType)
    {
        return s_globalObjectLabels.GetOrAdd(globalsType, static type =>
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            AddGlobalObjectLabels(type, labels, []);
            return labels;
        });
    }

    /// <summary>
    /// Рекурсивно добавляет публичные свойства и методы из графа globals-объектов в набор приоритетных меток.
    /// </summary>
    /// <param name="type">Тип, который проверяется на текущем шаге.</param>
    /// <param name="labels">Изменяемый набор меток, который заполняется в процессе обхода.</param>
    /// <param name="visited">Уже проверенные типы для защиты от циклов.</param>
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

    /// <summary>
    /// Внутреннее представление, связывающее Roslyn-теги и отформатированную документацию с публичным DTO подсказки.
    /// </summary>
    /// <param name="Tags">Roslyn-теги подсказки в нижнем регистре, используемые для фильтрации и ранжирования.</param>
    /// <param name="Documentation">Отформатированная Roslyn-документация для проверки видимости API и отображения в редакторе.</param>
    /// <param name="Item">Публичный элемент автодополнения, возвращаемый IntelliSense API.</param>
    private sealed record CompletionCandidate(
        IReadOnlyList<string> Tags,
        string? Documentation,
        CsxCompletionItem Item);

    /// <summary>
    /// Кандидат с рассчитанным один раз рангом и sort text для текущего запроса completion.
    /// </summary>
    private sealed record RankedCompletionCandidate(
        CompletionCandidate Candidate,
        int Priority,
        string RankedSortText)
    {
        public CsxCompletionItem Item => Candidate.Item;

        public static RankedCompletionCandidate Create(
            CompletionCandidate candidate,
            IReadOnlySet<string> globalObjectLabels)
        {
            var priority = GetCompletionPriority(candidate, globalObjectLabels);
            return new RankedCompletionCandidate(
                candidate,
                priority,
                GetRankedSortText(candidate, priority));
        }
    }
}
