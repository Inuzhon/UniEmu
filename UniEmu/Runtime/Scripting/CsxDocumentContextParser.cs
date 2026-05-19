using System.Text.RegularExpressions;
using UniEmu.Contracts.Enums;

namespace UniEmu.Runtime.Scripting;

/// <summary>
/// Извлекает контекст CSX-документа из URI, который передает редактор или language endpoint.
/// </summary>
public static class CsxDocumentContextParser
{
    /// <summary>
    /// Шаблон для извлечения идентификатора скрипта из пути вида <c>/scripts/{id}</c>.
    /// </summary>
    private static readonly Regex s_scriptIdPattern = new(
        @"(?:^|/)scripts/(?<id>[^/?/#]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Разбирает URI документа и возвращает идентификатор скрипта, имя файла, область видимости и эмулятор.
    /// </summary>
    /// <param name="uri">URI документа из клиента или <see langword="null"/> для временного скрипта.</param>
    /// <returns>Контекст CSX-документа с безопасными значениями по умолчанию.</returns>
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

    /// <summary>
    /// Разбирает query string URI в словарь параметров без учета регистра ключей.
    /// </summary>
    /// <param name="query">Часть URI после символа <c>?</c>.</param>
    /// <returns>Словарь decoded-параметров запроса.</returns>
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

/// <summary>
/// Контекст CSX-документа, необходимый для выбора области видимости и видимых скриптов.
/// </summary>
/// <param name="ScriptId">Идентификатор сохраненного скрипта или <see langword="null"/> для временного документа.</param>
/// <param name="ScriptName">Имя CSX-файла, используемое для диагностики и относительных загрузок.</param>
/// <param name="Scope">Область видимости скрипта.</param>
/// <param name="EmulatorId">Идентификатор эмулятора для scoped-скрипта или <see langword="null"/>.</param>
public sealed record CsxDocumentContext(
    string? ScriptId,
    string? ScriptName,
    ScriptScope Scope,
    string? EmulatorId);
