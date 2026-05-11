using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxLoadedScriptExpander
{
    private static readonly Regex s_loadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public ExpandedCsxScript Expand(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        var prefix = new List<string>();
        var globalsPrefix = BuildGlobalsPrefix(globalsType);
        if (!string.IsNullOrEmpty(globalsPrefix))
        {
            prefix.Add(globalsPrefix);
        }

        foreach (Match match in s_loadDirective.Matches(content))
        {
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
            return new ExpandedCsxScript(expandedContent, Math.Clamp(position, 0, expandedContent.Length));
        }

        var prefixText = string.Join(System.Environment.NewLine, prefix) + System.Environment.NewLine;
        return new ExpandedCsxScript(prefixText + expandedContent, Math.Clamp(position, 0, expandedContent.Length) + prefixText.Length);
    }

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

    public IEnumerable<string> GetLoadDirectivePaths(string content)
    {
        foreach (Match match in s_loadDirective.Matches(content))
        {
            yield return match.Groups["path"].Value;
        }
    }

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

    private static string RemoveLoadDirectives(string content)
    {
        return s_loadDirective.Replace(content, match => new string(' ', match.Length));
    }

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

public sealed record ExpandedCsxScript(string Content, int Position);
