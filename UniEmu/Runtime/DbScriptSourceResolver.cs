using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime;

public sealed class DbScriptSourceResolver(IReadOnlyDictionary<string, string> scripts) : SourceReferenceResolver
{
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

    public override Stream OpenRead(string resolvedPath)
    {
        if (!scripts.TryGetValue(TagScriptPath.Normalize(resolvedPath), out var content))
        {
            throw new FileNotFoundException($"Script '{resolvedPath}' was not found.");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    public override string? ResolveReference(string path, string? baseFilePath)
    {
        var normalized = NormalizePath(path, baseFilePath);
        return normalized is not null && scripts.ContainsKey(normalized) ? normalized : null;
    }

    public override bool Equals(object? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

public static class TagScriptPath
{
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
