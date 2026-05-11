using System.Text.RegularExpressions;
using UniEmu.Contracts.Enums;

namespace UniEmu.Runtime.Scripting;

public static class CsxDocumentContextParser
{
    private static readonly Regex s_scriptIdPattern = new(
        @"(?:^|/)scripts/(?<id>[^/?/#]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static CsxDocumentContext Parse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return new CsxDocumentContext(null, "script.csx", ScriptScope.Shared, null);
        }

        var value = Uri.UnescapeDataString(uri);
        var queryIndex = value.IndexOf('?', StringComparison.Ordinal);
        var query = queryIndex >= 0 ? value[(queryIndex + 1)..] : string.Empty;
        var path = queryIndex >= 0 ? value[..queryIndex] : value;
        var parameters = ParseQuery(query);

        var idMatch = s_scriptIdPattern.Match(path);
        var scriptId = idMatch.Success ? idMatch.Groups["id"].Value : null;
        var scriptName = parameters.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : Path.GetFileName(path);

        var scope = parameters.TryGetValue("scope", out var scopeValue)
                    && Enum.TryParse<ScriptScope>(scopeValue, ignoreCase: true, out var parsedScope)
            ? parsedScope
            : ScriptScope.Shared;

        parameters.TryGetValue("emulatorId", out var emulatorId);
        return new CsxDocumentContext(scriptId, scriptName, scope, string.IsNullOrWhiteSpace(emulatorId) ? null : emulatorId);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                result[Uri.UnescapeDataString(part)] = string.Empty;
                continue;
            }

            result[Uri.UnescapeDataString(part[..equals])] = Uri.UnescapeDataString(part[(equals + 1)..]);
        }

        return result;
    }
}

public sealed record CsxDocumentContext(
    string? ScriptId,
    string? ScriptName,
    ScriptScope Scope,
    string? EmulatorId);
