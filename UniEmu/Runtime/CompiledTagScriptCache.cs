using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace UniEmu.Runtime;

/// <summary>
/// Кэширует скомпилированные CSX-скрипты по содержимому, зависимостям и опциям компиляции.
/// </summary>
public sealed class CompiledTagScriptCache
{
    /// <summary>
    /// Максимальное количество скомпилированных скриптов, которое хранится в LRU-кэше.
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// Синхронизирует доступ к LRU-словарю, потому что порядок использования обновляется при чтении и записи.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Хранит готовые скомпилированные скрипты по детерминированному ключу содержимого и опций компиляции.
    /// </summary>
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Объединяет параллельные запросы на компиляцию одного и того же скрипта в одну фактическую компиляцию.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<Script<object?>>> _pendingCompilations = new(StringComparer.Ordinal);

    /// <summary>
    /// Создает кэш с ограничением количества скомпилированных скриптов.
    /// </summary>
    /// <param name="capacity">Максимальное количество записей в кэше.</param>
    public CompiledTagScriptCache(int capacity = 256)
    {
        this._capacity = Math.Max(1, capacity);
    }

    /// <summary>
    /// Текущее количество скомпилированных скриптов в кэше.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Возвращает скомпилированный скрипт из кэша или компилирует новую версию.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Содержимое входного CSX-файла.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="baseOptions">Базовые опции Roslyn scripting.</param>
    /// <param name="globalsType">Тип globals-объекта.</param>
    /// <param name="validateCompiledScript">Дополнительная проверка скомпилированного скрипта перед добавлением в кэш.</param>
    /// <returns>Скомпилированный Roslyn-скрипт.</returns>
    public Script<object?> GetOrAdd(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions baseOptions,
        Type globalsType,
        Action<Script<object?>>? validateCompiledScript = null)
    {
        var key = BuildKey(entryPath, content, visibleScripts, baseOptions, globalsType);
        if (TryGet(key, out var cached))
            return cached;

        var lazy = _pendingCompilations.GetOrAdd(
            key,
            _ => new Lazy<Script<object?>>(
                () => Compile(entryPath, content, visibleScripts, baseOptions, globalsType, validateCompiledScript),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var compiled = lazy.Value;
            Add(key, compiled);
            return compiled;
        }
        finally
        {
            _pendingCompilations.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Очищает кэш и незавершенные компиляции.
    /// </summary>
    public void Clear()
    {
        var hadEntries = false;
        lock (_gate)
        {
            hadEntries = _entries.Count > 0;
            _entries.Clear();
        }

        if (!_pendingCompilations.IsEmpty)
        {
            hadEntries = true;
            _pendingCompilations.Clear();
        }

        if (!hadEntries)
            return;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Пытается получить скрипт из готового кэша и обновляет время последнего использования записи.
    /// </summary>
    /// <param name="key">Ключ кэша, построенный по содержимому скрипта, зависимостям и опциям компиляции.</param>
    /// <param name="script">Найденный скомпилированный скрипт.</param>
    /// <returns><see langword="true"/>, если запись найдена в кэше.</returns>
    private bool TryGet(string key, out Script<object?> script)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.LastUsed = DateTimeOffset.UtcNow;
                script = entry.Script;
                return true;
            }
        }

        script = default!;
        return false;
    }

    /// <summary>
    /// Добавляет скомпилированный скрипт в кэш и удаляет наименее недавно использованную запись при превышении лимита.
    /// </summary>
    /// <param name="key">Ключ кэша, соответствующий скомпилированному скрипту.</param>
    /// <param name="script">Скомпилированный Roslyn-скрипт.</param>
    private void Add(string key, Script<object?> script)
    {
        lock (_gate)
        {
            _entries[key] = new CacheEntry(script, DateTimeOffset.UtcNow);
            if (_entries.Count <= _capacity)
            {
                return;
            }

            var oldest = _entries
                .OrderBy(pair => pair.Value.LastUsed)
                .First()
                .Key;

            _entries.Remove(oldest);
        }
    }

    /// <summary>
    /// Создает Roslyn-скрипт, проверяет ошибки компиляции и выполняет дополнительную проверку перед кэшированием.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла, используемый для разрешения относительных <c>#load</c>.</param>
    /// <param name="content">Нормализованное содержимое входного скрипта.</param>
    /// <param name="visibleScripts">Скрипты, доступные резолверу <c>#load</c>.</param>
    /// <param name="baseOptions">Базовые опции Roslyn scripting.</param>
    /// <param name="globalsType">Тип globals-объекта, доступного скрипту.</param>
    /// <param name="validateCompiledScript">Дополнительная проверка уже скомпилированного скрипта.</param>
    /// <returns>Скомпилированный скрипт, готовый к выполнению.</returns>
    private static Script<object?> Compile(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions baseOptions,
        Type globalsType,
        Action<Script<object?>>? validateCompiledScript)
    {
        var options = baseOptions
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
        var script = CSharpScript.Create<object?>(content, options, globalsType);
        var diagnostics = script.Compile();
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();

        if (errors.Count > 0)
            throw new CompilationErrorException("Script compilation failed.", errors.ToImmutableArray());

        validateCompiledScript?.Invoke(script);

        return script;
    }

    /// <summary>
    /// Строит стабильный ключ кэша по входному скрипту, подключенным скриптам, импортам, ссылкам и типу globals.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Содержимое входного CSX-файла.</param>
    /// <param name="visibleScripts">Снимок скриптов, доступных для <c>#load</c>.</param>
    /// <param name="options">Опции Roslyn scripting, влияющие на результат компиляции.</param>
    /// <param name="globalsType">Тип globals-объекта, влияющий на доступные символы.</param>
    /// <returns>SHA1-хэш входных данных компиляции в шестнадцатеричном виде.</returns>
    private static string BuildKey(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        ScriptOptions options,
        Type globalsType)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
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

    /// <summary>
    /// Добавляет UTF-8 представление строки в инкрементальный хэш ключа кэша.
    /// </summary>
    /// <param name="hash">Инкрементальный SHA1-хэш.</param>
    /// <param name="value">Строковое значение, участвующее в ключе.</param>
    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        //Span<byte> length = stackalloc byte[sizeof(int)];
        //BitConverter.TryWriteBytes(length, bytes.Length);
        //hash.AppendData(length);
        hash.AppendData(bytes);
    }

    /// <summary>
    /// Описывает одну запись LRU-кэша: скомпилированный скрипт и время его последнего использования.
    /// </summary>
    /// <param name="script">Скомпилированный Roslyn-скрипт.</param>
    /// <param name="lastUsed">Время последнего обращения к записи.</param>
    private sealed class CacheEntry(Script<object?> script, DateTimeOffset lastUsed)
    {
        /// <summary>
        /// Скомпилированный Roslyn-скрипт, который можно выполнять с разными globals-объектами.
        /// </summary>
        public Script<object?> Script { get; } = script;

        /// <summary>
        /// Время последнего обращения к записи; используется для вытеснения старых скриптов.
        /// </summary>
        public DateTimeOffset LastUsed { get; set; } = lastUsed;
    }
}
