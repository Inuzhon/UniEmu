namespace UniEmu.Runtime.Scripting;

internal static class CsxNullableContext
{
    private const string DefaultNullableDirective = "#nullable enable";

    public static string Apply(string content, string path)
    {
        var normalizedPath = TagScriptPath.Normalize(string.IsNullOrWhiteSpace(path) ? "script.csx" : path);
        return string.Join(
            System.Environment.NewLine,
            DefaultNullableDirective,
            $"#line 1 \"{EscapeLineDirectivePath(normalizedPath)}\"",
            content);
    }

    public static IReadOnlyDictionary<string, string> ApplyToScripts(IReadOnlyDictionary<string, string> scripts)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, content) in scripts)
        {
            var normalizedPath = TagScriptPath.Normalize(path);
            result[normalizedPath] = Apply(content, normalizedPath);
        }

        return result;
    }

    private static string EscapeLineDirectivePath(string path)
    {
        return path
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
