using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxDocumentStore(IServiceScopeFactory scopeFactory)
{
    private static readonly Regex ScriptIdPattern = new(
        @"(?:^|/)scripts/(?<id>[^/?/#]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ConcurrentDictionary<string, CsxOpenDocument> documents = new(StringComparer.OrdinalIgnoreCase);

    public void Open(string uri, string text, int? version)
    {
        documents[uri] = new CsxOpenDocument(uri, text, version);
    }

    public void Update(string uri, string text, int? version)
    {
        documents[uri] = new CsxOpenDocument(uri, text, version);
    }

    public void Close(string uri)
    {
        documents.TryRemove(uri, out _);
    }

    public bool TryGet(string uri, out CsxOpenDocument document) => documents.TryGetValue(uri, out document!);

    public async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(string uri, CancellationToken cancellationToken)
    {
        var context = ParseContext(uri);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UniEmuDbContext>();
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);

        var query = db.ScriptFiles
            .AsNoTracking()
            .Where(script => script.Scope == sharedScope || script.EmulatorId == context.EmulatorId);

        var scripts = await query
            .OrderBy(script => script.Scope == sharedScope ? 0 : 1)
            .ThenBy(script => script.Name)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts)
        {
            result[TagScriptPath.Normalize(script.Name)] = script.Content;
        }

        foreach (var document in documents.Values)
        {
            var documentContext = ParseContext(document.Uri);
            if (documentContext.ScriptName is null)
            {
                continue;
            }

            if (documentContext.Scope == ScriptScope.Shared || documentContext.EmulatorId == context.EmulatorId)
            {
                result[TagScriptPath.Normalize(documentContext.ScriptName)] = document.Text;
            }
        }

        return result;
    }

    public CsxDocumentContext ParseContext(string uri)
    {
        var value = Uri.UnescapeDataString(uri);
        var queryIndex = value.IndexOf('?', StringComparison.Ordinal);
        var query = queryIndex >= 0 ? value[(queryIndex + 1)..] : string.Empty;
        var path = queryIndex >= 0 ? value[..queryIndex] : value;
        var parameters = ParseQuery(query);

        var idMatch = ScriptIdPattern.Match(path);
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

public sealed record CsxOpenDocument(string Uri, string Text, int? Version);

public sealed record CsxDocumentContext(
    string? ScriptId,
    string? ScriptName,
    ScriptScope Scope,
    string? EmulatorId);
