using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Runtime.Scripting;

/// <summary>
/// Сворачивает видимые CSX-скрипты в словарь путей с единым правилом приоритета.
/// </summary>
public static class VisibleScriptResolver
{
    /// <summary>
    /// Создает словарь содержимого по нормализованному пути, где scoped-скрипт эмулятора перекрывает общий скрипт.
    /// </summary>
    /// <param name="scripts">Список скриптов, видимых эмулятору.</param>
    /// <param name="validateScript">Дополнительная проверка содержимого каждого скрипта.</param>
    /// <returns>Словарь содержимого скриптов по нормализованным путям.</returns>
    public static Dictionary<string, string> ToContentMap(
        IEnumerable<ScriptFileEntity> scripts,
        Action<ScriptFileEntity>? validateScript = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts
                     .OrderBy(ScopePriority)
                     .ThenBy(script => script.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(script => script.Id, StringComparer.OrdinalIgnoreCase))
        {
            validateScript?.Invoke(script);
            result[TagScriptPath.Normalize(script.Name)] = script.Content;
        }

        return result;
    }

    /// <summary>
    /// Добавляет или заменяет содержимое скрипта в словаре по нормализованному пути.
    /// </summary>
    /// <param name="scripts">Словарь содержимого скриптов.</param>
    /// <param name="name">Имя или путь скрипта.</param>
    /// <param name="content">Содержимое скрипта.</param>
    public static void AddOrReplace(Dictionary<string, string> scripts, string name, string content)
    {
        scripts[TagScriptPath.Normalize(name)] = content;
    }

    private static int ScopePriority(ScriptFileEntity script)
    {
        return string.Equals(script.Scope, UniEmuJson.EnumString(ScriptScope.Shared), StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;
    }
}
