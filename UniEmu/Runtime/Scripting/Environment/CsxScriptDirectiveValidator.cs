using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptDirectiveValidator(CsxLoadedScriptExpander expander)
{
    private static readonly Regex s_blockedDirective = new(
        @"^\s*#\s*(r|using)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public CsxScriptDirectiveValidator()
        : this(new CsxLoadedScriptExpander())
    {
    }

    public void ValidateSupportedDirectives(string content)
    {
        var match = s_blockedDirective.Match(content);
        if (match.Success)
        {
            throw new InvalidOperationException($"Unsupported script directive '{match.Value.Trim()}'. Use #load for shared scripts.");
        }
    }

    public void DetectLoadCycles(string entryPath, IReadOnlyDictionary<string, string> scripts)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(TagScriptPath.Normalize(entryPath), visited, stack, scripts);
    }

    private void Visit(
        string path,
        HashSet<string> visited,
        HashSet<string> stack,
        IReadOnlyDictionary<string, string> scripts)
    {
        if (stack.Contains(path))
        {
            throw new InvalidOperationException($"Cyclic #load detected at '{path}'.");
        }

        if (!visited.Add(path) || !scripts.TryGetValue(path, out var content))
        {
            return;
        }

        stack.Add(path);
        foreach (var loadPathValue in expander.GetLoadDirectivePaths(content))
        {
            var loadPath = expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is not null)
            {
                Visit(loadPath, visited, stack, scripts);
            }
        }

        stack.Remove(path);
    }
}
