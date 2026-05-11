using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting;

public static class TagScriptContentNormalizer
{
    private static readonly Regex s_finalReturnStatement = new(
        @"\breturn\s+(?<expression>.+?)\s*;\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static string NormalizeEntryScriptContent(string content)
    {
        var match = s_finalReturnStatement.Match(content);
        return match.Success
            ? content[..match.Index] + match.Groups["expression"].Value
            : content;
    }
}
