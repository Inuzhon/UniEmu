using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Environment;

/// <summary>
/// Подготавливает CSX-документ для Roslyn workspace: подставляет загруженные скрипты и скрытый globals-префикс.
/// </summary>
public sealed class CsxLoadedScriptExpander
{
    /// <summary>
    /// Регулярное выражение для поиска директив <c>#load "path"</c> в CSX-документе.
    /// </summary>
    private static readonly Regex s_loadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Разворачивает входной документ в один текст для language features, сохраняя позицию курсора в исходном документе.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Исходный текст входного файла.</param>
    /// <param name="position">Позиция курсора в исходном тексте.</param>
    /// <param name="visibleScripts">Скрипты, доступные для директив <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта, свойства которого должны быть видимы как top-level переменные.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Развернутый CSX-текст и скорректированная позиция курсора.</returns>
    public ExpandedCsxScript Expand(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = new List<string>();
        var globalsPrefix = BuildGlobalsPrefix(globalsType);
        if (!string.IsNullOrEmpty(globalsPrefix))
        {
            prefix.Add(globalsPrefix);
        }

        foreach (Match match in s_loadDirective.Matches(content))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var loadPath = ResolveLoadPath(match.Groups["path"].Value, entryPath, visibleScripts);
            if (loadPath is null || !visibleScripts.TryGetValue(loadPath, out var loadedContent))
            {
                continue;
            }

            prefix.Add($"#line 1 \"{loadPath}\"");
            prefix.Add(loadedContent);
            prefix.Add("#line default");
        }

        var expandedContent = RemoveLoadDirectives(content);
        if (prefix.Count == 0)
        {
            return new ExpandedCsxScript(expandedContent, Math.Clamp(position, 0, expandedContent.Length), 0);
        }

        var prefixText = string.Join(System.Environment.NewLine, prefix) + System.Environment.NewLine;
        return new ExpandedCsxScript(
            prefixText + expandedContent,
            Math.Clamp(position, 0, expandedContent.Length) + prefixText.Length,
            prefixText.Length);
    }

    /// <summary>
    /// Разрешает путь из директивы <c>#load</c> относительно входного файла и словаря видимых скриптов.
    /// </summary>
    /// <param name="path">Путь из директивы <c>#load</c>.</param>
    /// <param name="baseFilePath">Путь файла, в котором находится директива.</param>
    /// <param name="scripts">Словарь доступных скриптов по нормализованному пути.</param>
    /// <returns>Нормализованный путь найденного скрипта или <see langword="null"/>.</returns>
    public string? ResolveLoadPath(
        string path,
        string baseFilePath,
        IReadOnlyDictionary<string, string> scripts)
    {
        var normalized = TagScriptPath.Normalize(path);
        if (scripts.ContainsKey(normalized))
        {
            return normalized;
        }

        var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var relative = TagScriptPath.Normalize($"{baseDir}/{path}");
        return scripts.ContainsKey(relative) ? relative : null;
    }

    /// <summary>
    /// Возвращает пути всех директив <c>#load</c>, найденных в тексте скрипта.
    /// </summary>
    /// <param name="content">Текст CSX-скрипта.</param>
    /// <returns>Последовательность путей, указанных в директивах загрузки.</returns>
    public IEnumerable<string> GetLoadDirectivePaths(string content)
    {
        foreach (Match match in s_loadDirective.Matches(content))
        {
            yield return match.Groups["path"].Value;
        }
    }

    /// <summary>
    /// Создает скрытый префикс с top-level объявлениями свойств globals-типа для корректной работы language features.
    /// </summary>
    /// <param name="globalsType">Тип globals-объекта пользовательского скрипта.</param>
    /// <returns>Текст скрытого префикса или пустая строка.</returns>
    private static string BuildGlobalsPrefix(Type globalsType)
    {
        var properties = globalsType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToArray();

        if (properties.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#line hidden");
        foreach (var property in properties)
        {
            builder.Append(GetTypeName(property.PropertyType));
            builder.Append(' ');
            builder.Append(property.Name);
            builder.AppendLine(" = default!;");
        }

        builder.Append("#line default");
        return builder.ToString();
    }

    /// <summary>
    /// Заменяет директивы <c>#load</c> пробелами, чтобы сохранить координаты остального входного текста.
    /// </summary>
    /// <param name="content">Исходный CSX-текст.</param>
    /// <returns>Текст без активных директив <c>#load</c>.</returns>
    private static string RemoveLoadDirectives(string content)
    {
        return s_loadDirective.Replace(content, match => new string(' ', match.Length));
    }

    /// <summary>
    /// Возвращает имя CLR-типа в форме, пригодной для вставки в C#-код скрытого префикса.
    /// </summary>
    /// <param name="type">Тип свойства globals-объекта.</param>
    /// <returns>Полное имя типа с generic-аргументами.</returns>
    private static string GetTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericType = type.GetGenericTypeDefinition();
        var genericName = genericType.FullName ?? genericType.Name;
        var tickIndex = genericName.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex >= 0)
        {
            genericName = genericName[..tickIndex];
        }

        return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeName))}>";
    }
}

/// <summary>
/// Результат разворачивания CSX-документа для Roslyn language features.
/// </summary>
/// <param name="Content">Развернутый текст с загруженными скриптами и скрытым globals-префиксом.</param>
/// <param name="Position">Позиция курсора в развернутом тексте.</param>
/// <param name="EntryContentStart">Offset, с которого начинается исходный входной документ в развернутом тексте.</param>
public sealed record ExpandedCsxScript(string Content, int Position, int EntryContentStart);
