using System.Text.RegularExpressions;
using UniEmu.Runtime.Scripting;

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

    public IReadOnlyList<CsxDiagnostic> GetUnsupportedDirectiveDiagnostics(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> scripts)
    {
        var diagnostics = new List<CsxDiagnostic>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        VisitForDiagnostics(TagScriptPath.Normalize(entryPath), content, visited, scripts, diagnostics);
        return diagnostics;
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

    private void VisitForDiagnostics(
        string path,
        string content,
        HashSet<string> visited,
        IReadOnlyDictionary<string, string> scripts,
        List<CsxDiagnostic> diagnostics)
    {
        if (!visited.Add(path))
        {
            return;
        }

        foreach (Match match in s_blockedDirective.Matches(content))
        {
            diagnostics.Add(CreateUnsupportedDirectiveDiagnostic(content, match));
        }

        foreach (var loadPathValue in expander.GetLoadDirectivePaths(content))
        {
            var loadPath = expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is not null && scripts.TryGetValue(loadPath, out var loadedContent))
            {
                VisitForDiagnostics(loadPath, loadedContent, visited, scripts, diagnostics);
            }
        }
    }

    private static CsxDiagnostic CreateUnsupportedDirectiveDiagnostic(string content, Match match)
    {
        var start = GetLinePosition(content, match.Index);
        var end = GetLinePosition(content, match.Index + match.Length);
        return new CsxDiagnostic(
            "CSX001",
            $"Unsupported script directive '{match.Value.Trim()}'. Use #load for shared scripts.",
            CsxDiagnosticSeverity.Error,
            start.Line,
            start.Character,
            end.Line,
            end.Character);
    }

    private static (int Line, int Character) GetLinePosition(string content, int offset)
    {
        var line = 0;
        var lineStart = 0;
        var safeOffset = Math.Clamp(offset, 0, content.Length);

        for (var i = 0; i < safeOffset; i++)
        {
            if (content[i] != '\n')
            {
                continue;
            }

            line++;
            lineStart = i + 1;
        }

        return (line, safeOffset - lineStart);
    }
}
