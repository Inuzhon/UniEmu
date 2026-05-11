using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace UniEmu.Runtime;

public sealed class CompiledTagScriptCache
{
    private readonly int capacity;
    private readonly object gate = new();
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Script<object?>>> pendingCompilations = new(StringComparer.Ordinal);

    public CompiledTagScriptCache(int capacity = 256)
    {
        this.capacity = Math.Max(1, capacity);
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    public Script<object?> GetOrAdd(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions baseOptions,
        Type globalsType)
    {
        var key = BuildKey(entryPath, content, visibleScripts, baseOptions, globalsType);
        if (TryGet(key, out var cached))
        {
            return cached;
        }

        var lazy = pendingCompilations.GetOrAdd(
            key,
            _ => new Lazy<Script<object?>>(
                () => Compile(entryPath, content, visibleScripts, baseOptions, globalsType),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var compiled = lazy.Value;
            Add(key, compiled);
            return compiled;
        }
        finally
        {
            pendingCompilations.TryRemove(key, out _);
        }
    }

    public void Clear()
    {
        var hadEntries = false;
        lock (gate)
        {
            hadEntries = entries.Count > 0;
            entries.Clear();
        }

        if (!pendingCompilations.IsEmpty)
        {
            hadEntries = true;
            pendingCompilations.Clear();
        }

        if (!hadEntries)
        {
            return;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private bool TryGet(string key, out Script<object?> script)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                entry.LastUsed = DateTimeOffset.UtcNow;
                script = entry.Script;
                return true;
            }
        }

        script = default!;
        return false;
    }

    private void Add(string key, Script<object?> script)
    {
        lock (gate)
        {
            entries[key] = new CacheEntry(script, DateTimeOffset.UtcNow);
            if (entries.Count <= capacity)
            {
                return;
            }

            var oldest = entries
                .OrderBy(pair => pair.Value.LastUsed)
                .First()
                .Key;

            entries.Remove(oldest);
        }
    }

    private static Script<object?> Compile(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions baseOptions,
        Type globalsType)
    {
        var options = baseOptions
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
        var script = CSharpScript.Create<object?>(content, options, globalsType);
        var diagnostics = script.Compile();
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new CompilationErrorException("Script compilation failed.", errors.ToImmutableArray());
        }

        return script;
    }

    private static string BuildKey(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions options,
        Type globalsType)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "entry");
        Append(hash, TagScriptPath.Normalize(entryPath));
        Append(hash, content);
        Append(hash, "globals");
        Append(hash, globalsType.AssemblyQualifiedName ?? globalsType.FullName ?? globalsType.Name);
        Append(hash, "imports");
        foreach (var import in options.Imports.Order(StringComparer.Ordinal))
        {
            Append(hash, import);
        }

        Append(hash, "references");
        foreach (var reference in options.MetadataReferences.Select(reference => reference.Display ?? string.Empty).Order(StringComparer.Ordinal))
        {
            Append(hash, reference);
        }

        Append(hash, "visible-scripts");
        foreach (var script in visibleScripts.OrderBy(script => script.Key, StringComparer.OrdinalIgnoreCase))
        {
            Append(hash, TagScriptPath.Normalize(script.Key));
            Append(hash, script.Value);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed class CacheEntry(Script<object?> script, DateTimeOffset lastUsed)
    {
        public Script<object?> Script { get; } = script;
        public DateTimeOffset LastUsed { get; set; } = lastUsed;
    }
}
