using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime;

/// <summary>
/// Разрешает <c>#load</c>-ссылки CSX-скриптов из набора скриптов, загруженных из базы.
/// </summary>
public sealed class DbScriptSourceResolver(IReadOnlyDictionary<string, string> scripts) : SourceReferenceResolver
{
    /// <summary>
    /// Нормализует путь скрипта и учитывает относительный путь базового файла.
    /// </summary>
    /// <param name="path">Путь из директивы <c>#load</c>.</param>
    /// <param name="baseFilePath">Путь файла, из которого выполняется загрузка.</param>
    /// <returns>Нормализованный путь скрипта.</returns>
    public override string? NormalizePath(string path, string? baseFilePath)
    {
        var normalized = TagScriptPath.Normalize(path);
        if (scripts.ContainsKey(normalized))
        {
            return normalized;
        }

        if (!string.IsNullOrWhiteSpace(baseFilePath))
        {
            var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var relative = TagScriptPath.Normalize($"{baseDir}/{path}");
                if (scripts.ContainsKey(relative))
                {
                    return relative;
                }
            }
        }

        return normalized;
    }

    /// <summary>
    /// Открывает содержимое скрипта как UTF-8 поток.
    /// </summary>
    /// <param name="resolvedPath">Разрешенный путь скрипта.</param>
    /// <returns>Поток с содержимым скрипта.</returns>
    public override Stream OpenRead(string resolvedPath)
    {
        if (!scripts.TryGetValue(TagScriptPath.Normalize(resolvedPath), out var content))
        {
            throw new FileNotFoundException($"Script '{resolvedPath}' was not found.");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// Разрешает ссылку на скрипт только если он присутствует в видимом наборе.
    /// </summary>
    /// <param name="path">Путь из директивы <c>#load</c>.</param>
    /// <param name="baseFilePath">Путь файла, из которого выполняется загрузка.</param>
    /// <returns>Разрешенный путь или <see langword="null"/>.</returns>
    public override string? ResolveReference(string path, string? baseFilePath)
    {
        var normalized = NormalizePath(path, baseFilePath);
        return normalized is not null && scripts.ContainsKey(normalized) ? normalized : null;
    }

    public override bool Equals(object? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Нормализует пути CSX-скриптов к единому slash-формату.
/// </summary>
public static class TagScriptPath
{
    /// <summary>
    /// Убирает начальные <c>./</c> и приводит разделители пути к <c>/</c>.
    /// </summary>
    /// <param name="path">Исходный путь скрипта.</param>
    /// <returns>Нормализованный путь скрипта.</returns>
    public static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }
}
