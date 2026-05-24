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
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            var match = matches[index];
            if (!string.IsNullOrWhiteSpace(content[(match.Index + match.Length)..]))
                continue;

            return content[..match.Index] + match.Groups["expression"].Value;
        }

        return content;
    }
}
