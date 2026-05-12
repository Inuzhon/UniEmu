using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting;

public static class TagScriptContentNormalizer
{
    private static readonly Regex s_finalReturnStatement = new(
        @"(?m)^(?<indent>\s*)return\s+(?<expression>[^;\r\n]+)\s*;\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeEntryScriptContent(string content)
    {
        var matches = s_finalReturnStatement.Matches(content);
        var match = matches.Count == 0 ? Match.Empty : matches[^1];
        return match.Success
            ? content[..match.Index] + match.Groups["expression"].Value
            : content;
    }
}
