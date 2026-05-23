using System.Globalization;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Hosting;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime;

/// <summary>
/// Выполняет CSX-скрипты тегов с доступом к runtime-состоянию, тегам и разрешенным API.
/// </summary>
public sealed class TagScriptExecutionService
{
    /// <summary>
    /// Создает сервис выполнения скриптов с настройками CSX по умолчанию.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="dataCache">Кэш данных эмуляторов и видимых скриптов.</param>
    /// <param name="stateStore">Хранилище runtime-значений тегов.</param>
    /// <param name="scriptCache">Кэш скомпилированных CSX-скриптов.</param>
    /// <param name="previewFlushService">Сервис отложенной записи preview static-тегов.</param>
    public TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        TagPreviewFlushService? previewFlushService = null)
        : this(
            db,
            dataCache,
            stateStore,
            scriptCache,
            new CsxScriptEnvironment(),
            new CsxScriptDirectiveValidator(),
            new CsxScriptSecurityValidator(),
            previewFlushService)
    {
    }

    /// <summary>
    /// Создает сервис выполнения скриптов с явно заданными компонентами CSX-окружения.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="dataCache">Кэш данных эмуляторов и видимых скриптов.</param>
    /// <param name="stateStore">Хранилище runtime-значений тегов.</param>
    /// <param name="scriptCache">Кэш скомпилированных CSX-скриптов.</param>
    /// <param name="scriptEnvironment">Настройки Roslyn scripting.</param>
    /// <param name="directiveValidator">Валидатор CSX-директив.</param>
    /// <param name="securityValidator">Валидатор запрещенных API.</param>
    /// <param name="previewFlushService">Сервис отложенной записи preview static-тегов.</param>
    public TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        CsxScriptEnvironment scriptEnvironment,
        CsxScriptDirectiveValidator directiveValidator,
        CsxScriptSecurityValidator securityValidator,
        TagPreviewFlushService? previewFlushService = null)
        : this(db, dataCache, stateStore, scriptCache, scriptEnvironment, directiveValidator, securityValidator, null, previewFlushService)
    {
    }

    /// <summary>
    /// Создает сервис выполнения скриптов с тестовой или альтернативной реализацией REST-операций.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="dataCache">Кэш данных эмуляторов и видимых скриптов.</param>
    /// <param name="stateStore">Хранилище последних runtime-значений тегов.</param>
    /// <param name="scriptCache">Кэш скомпилированных CSX-скриптов.</param>
    /// <param name="restOperations">REST API, доступный пользовательским скриптам.</param>
    /// <param name="previewFlushService">Сервис отложенной записи preview static-тегов.</param>
    /// <param name="scriptExecutionTimeout">Максимальное время ожидания выполнения пользовательского CSX-скрипта.</param>
    internal TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        ITagScriptRestOperations? restOperations,
        TagPreviewFlushService? previewFlushService = null,
        TimeSpan? scriptExecutionTimeout = null)
        : this(
            db,
            dataCache,
            stateStore,
            scriptCache,
            new CsxScriptEnvironment(),
            new CsxScriptDirectiveValidator(),
            new CsxScriptSecurityValidator(),
            restOperations,
            previewFlushService,
            scriptExecutionTimeout)
    {
    }

    /// <summary>
    /// Создает сервис выполнения скриптов со всеми явно заданными зависимостями CSX-runtime.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="dataCache">Кэш данных эмуляторов и видимых скриптов.</param>
    /// <param name="stateStore">Хранилище последних runtime-значений тегов.</param>
    /// <param name="scriptCache">Кэш скомпилированных CSX-скриптов.</param>
    /// <param name="scriptEnvironment">Фабрика parse options, metadata references и imports для Roslyn scripting.</param>
    /// <param name="directiveValidator">Валидатор поддерживаемых директив и циклов <c>#load</c>.</param>
    /// <param name="securityValidator">Валидатор запрещенных типов и вызовов в пользовательских скриптах.</param>
    /// <param name="restOperations">REST API, доступный пользовательским скриптам, или <see langword="null"/> для отключенного REST-контекста.</param>
    /// <param name="previewFlushService">Сервис отложенной записи preview static-тегов.</param>
    /// <param name="scriptExecutionTimeout">Максимальное время ожидания выполнения пользовательского CSX-скрипта.</param>
    internal TagScriptExecutionService(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        CompiledTagScriptCache scriptCache,
        CsxScriptEnvironment scriptEnvironment,
        CsxScriptDirectiveValidator directiveValidator,
        CsxScriptSecurityValidator securityValidator,
        ITagScriptRestOperations? restOperations,
        TagPreviewFlushService? previewFlushService = null,
        TimeSpan? scriptExecutionTimeout = null)
    {
        this.db = db;
        this.dataCache = dataCache;
        this.stateStore = stateStore;
        this.scriptCache = scriptCache;
        this.scriptEnvironment = scriptEnvironment;
        this.directiveValidator = directiveValidator;
        this.securityValidator = securityValidator;
        this.restOperations = restOperations;
        this.previewFlushService = previewFlushService;
        this.scriptExecutionTimeout = scriptExecutionTimeout ?? DefaultScriptExecutionTimeout;

        if (this.scriptExecutionTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(scriptExecutionTimeout), "CSX script execution timeout must be positive.");
    }

    /// <summary>
    /// Максимальное время ожидания выполнения одного пользовательского CSX-скрипта.
    /// </summary>
    private static readonly TimeSpan DefaultScriptExecutionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// EF Core-контекст текущего scope, используемый для чтения и сохранения persistent state скриптов.
    /// </summary>
    private readonly UniEmuDbContext db;

    /// <summary>
    /// Кэш конфигурации эмуляторов, видимых скриптов и CNC-программ, разделяемый runtime-сервисами.
    /// </summary>
    private readonly CachedUniEmuDataService dataCache;

    /// <summary>
    /// In-memory хранилище последних рассчитанных значений тегов.
    /// </summary>
    private readonly TagRuntimeStateStore stateStore;

    /// <summary>
    /// Кэш скомпилированных Roslyn-скриптов, уменьшающий повторные компиляции одинакового CSX-кода.
    /// </summary>
    private readonly CompiledTagScriptCache scriptCache;

    /// <summary>
    /// Окружение Roslyn scripting: imports, metadata references, parse и compilation options.
    /// </summary>
    private readonly CsxScriptEnvironment scriptEnvironment;

    /// <summary>
    /// Проверяет допустимость директив CSX и графа подключенных скриптов.
    /// </summary>
    private readonly CsxScriptDirectiveValidator directiveValidator;

    /// <summary>
    /// Проверяет скомпилированный скрипт на запрещенные API перед попаданием в кэш выполнения.
    /// </summary>
    private readonly CsxScriptSecurityValidator securityValidator;

    /// <summary>
    /// REST-операции, доступные скриптам через <c>UniEmu.Rest</c>; при отсутствии REST-контекст создается отключенным.
    /// </summary>
    private readonly ITagScriptRestOperations? restOperations;

    /// <summary>
    /// Буфер отложенной записи preview для static-тегов, измененных из пользовательского скрипта.
    /// </summary>
    private readonly TagPreviewFlushService? previewFlushService;

    /// <summary>
    /// Budget ожидания выполнения Roslyn script до возврата управляемой ошибки вызывающему коду.
    /// </summary>
    private readonly TimeSpan scriptExecutionTimeout;

    /// <summary>
    /// Компилирует и выполняет скрипт тега, обновляя persistent state при изменениях.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого выполняется скрипт.</param>
    /// <param name="tag">Тег, связанный со скриптом.</param>
    /// <param name="timestamp">Время расчета значения.</param>
    /// <param name="cancellationToken">Токен отмены выполнения.</param>
    /// <param name="currentValue">Текущее значение, переданное в formula-script после генератора.</param>
    /// <returns>Рассчитанное значение тега.</returns>
    public async Task<GeneratedTagValue> GenerateScriptTagAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        object? currentValue = null)
    {
        var script = await ResolveEntryScriptAsync(emulator.Id, tag, cancellationToken);
        var entryContent = TagScriptContentNormalizer.NormalizeEntryScriptContent(script.Content);
        directiveValidator.ValidateSupportedDirectives(entryContent);

        var scripts = await LoadVisibleScriptsAsync(emulator.Id, cancellationToken);
        scripts[script.Path] = entryContent;
        directiveValidator.DetectLoadCycles(script.Path, scripts);

        var state = await GetOrCreateStateAsync(emulator.Id, script.StateKey, cancellationToken);
        var stateValues = UniEmuJson.Deserialize<Dictionary<string, object?>>(state.ValuesJson)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var globals = BuildGlobals(emulator, tag, timestamp, stateValues, cancellationToken, currentValue);
        var scriptOptions = scriptEnvironment.CreateScriptOptions(script.Path, scripts, typeof(TagScriptGlobals));
        var compiledScript = scriptCache.GetOrAdd(
            script.Path,
            entryContent,
            scripts,
            scriptOptions,
            typeof(TagScriptGlobals),
            compiled => ValidateSecurity(compiled, cancellationToken));
        var scriptState = await RunScriptWithTimeoutAsync(compiledScript, script.Path, globals, cancellationToken);
        var result = scriptState.ReturnValue;

        if (globals.UniEmu.State.IsDirty || globals.UniEmu.Tags.IsDirty)
        {
            state.ValuesJson = UniEmuJson.Serialize(globals.UniEmu.State.Snapshot());
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = TelemetryValueGenerator.ApplyTagRounding(tagType, tag, CastResult(tagType, result, tag.Preview));
        SpecialParameter? specialParameter = string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);

        return new GeneratedTagValue(tag.Key, tag.Name, value, TelemetryValueGenerator.ToNumericValue(value), specialParameter);
    }

    /// <summary>
    /// Определяет входной CSX-скрипт для тега: inline-код, выбранный файл скрипта или безопасную пустую заглушку.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора, задающий область видимости скриптов.</param>
    /// <param name="tag">Тег с formula-конфигурацией источника скрипта.</param>
    /// <param name="cancellationToken">Токен отмены запроса к кэшу и базе данных.</param>
    /// <returns>Содержимое входного скрипта, его путь и ключ persistent state.</returns>
    private async Task<ScriptContent> ResolveEntryScriptAsync(
        string emulatorId,
        EmulatorTagEntity tag,
        CancellationToken cancellationToken)
    {
        var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);

        if (!string.IsNullOrWhiteSpace(formula?.InlineScript))
            return new ScriptContent($"inline/{tag.Id}.csx", formula.InlineScript, $"inline:{tag.Id}");

        if (string.IsNullOrWhiteSpace(formula?.ScriptId))
            return new ScriptContent($"inline/{tag.Id}.csx", "return null;", $"inline:{tag.Id}");

        var visibleScripts = await dataCache.GetVisibleScriptsAsync(emulatorId, cancellationToken);
        var script = visibleScripts.FirstOrDefault(s => s.Id == formula.ScriptId);

        if (script is null)
            throw new InvalidOperationException($"Script '{formula.ScriptId}' was not found for tag '{tag.Name}'.");

        return new ScriptContent(TagScriptPath.Normalize(script.Name), script.Content, $"script:{script.Id}");
    }

    /// <summary>
    /// Загружает все скрипты, доступные эмулятору для директив <c>#load</c>, и нормализует их пути.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора, для которого выбираются shared и scoped-скрипты.</param>
    /// <param name="cancellationToken">Токен отмены чтения данных.</param>
    /// <returns>Словарь содержимого скриптов по нормализованному пути.</returns>
    private async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var scripts = await dataCache.GetVisibleScriptsAsync(emulatorId, cancellationToken);
        return VisibleScriptResolver.ToContentMap(
            scripts,
            script => directiveValidator.ValidateSupportedDirectives(script.Content));
    }

    /// <summary>
    /// Проверяет compilation скомпилированного скрипта на запрещенные конструкции и прерывает выполнение при нарушениях.
    /// </summary>
    /// <param name="script">Скомпилированный Roslyn-скрипт, который еще не добавлен в runtime-кэш.</param>
    /// <param name="cancellationToken">Токен отмены проверки.</param>
    private void ValidateSecurity(Script<object?> script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = securityValidator.Validate(script.GetCompilation());
        if (diagnostics.Count > 0)
        {
            throw new CsxScriptValidationException(diagnostics);
        }
    }

    /// <summary>
    /// Выполняет Roslyn script с detection timeout вокруг <see cref="Script{T}.RunAsync(object?, Func{Exception, bool}?, CancellationToken)"/>.
    /// </summary>
    /// <param name="script">Скомпилированный Roslyn script.</param>
    /// <param name="scriptPath">Путь входного CSX-скрипта для диагностики timeout.</param>
    /// <param name="globals">Globals-объект выполнения скрипта.</param>
    /// <param name="cancellationToken">Токен отмены внешнего runtime-запроса.</param>
    /// <returns>Состояние завершенного скрипта.</returns>
    private async Task<ScriptState<object?>> RunScriptWithTimeoutAsync(
        Script<object?> script,
        string scriptPath,
        TagScriptGlobals globals,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Roslyn не умеет безопасно прерывать CPU-bound user code. Background thread дает caller'у
        // вернуться по timeout, но некооперативный script может продолжить работу до завершения процесса.
        var executionTask = StartScriptRun(script, globals, cancellationToken);

        try
        {
            return await executionTask.WaitAsync(scriptExecutionTimeout, cancellationToken);
        }
        catch (TimeoutException ex) when (!executionTask.IsCompleted)
        {
            throw new TimeoutException(
                $"CSX script '{scriptPath}' timed out after {scriptExecutionTimeout.TotalSeconds:N1} seconds.",
                ex);
        }
    }

    /// <summary>
    /// Запускает script.RunAsync на отдельном background thread, чтобы CPU-bound script не блокировал caller thread.
    /// </summary>
    /// <param name="script">Скомпилированный Roslyn script.</param>
    /// <param name="globals">Globals-объект выполнения скрипта.</param>
    /// <param name="cancellationToken">Токен отмены, передаваемый в Roslyn script.</param>
    /// <returns>Task, завершающийся вместе с Roslyn script.</returns>
    private static Task<ScriptState<object?>> StartScriptRun(
        Script<object?> script,
        TagScriptGlobals globals,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<ScriptState<object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var scriptState = script.RunAsync(globals, cancellationToken).GetAwaiter().GetResult();
                completion.TrySetResult(scriptState);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "UniEmu CSX script execution",
        };

        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// Возвращает persistent state для пары эмулятор-скрипт или создает новую пустую запись в текущем контексте.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора, которому принадлежит state.</param>
    /// <param name="scriptKey">Стабильный ключ state для inline-скрипта или файла скрипта.</param>
    /// <param name="cancellationToken">Токен отмены запроса к базе данных.</param>
    /// <returns>Сущность persistent state, отслеживаемая текущим DbContext.</returns>
    private async Task<ScriptRuntimeStateEntity> GetOrCreateStateAsync(
        string emulatorId,
        string scriptKey,
        CancellationToken cancellationToken)
    {
        var state = await db.ScriptRuntimeStates
            .FirstOrDefaultAsync(s => s.EmulatorId == emulatorId && s.ScriptKey == scriptKey, cancellationToken);

        if (state is not null)
            return state;

        state = new ScriptRuntimeStateEntity
        {
            Id = $"srs-{Guid.NewGuid():N}"[..13],
            EmulatorId = emulatorId,
            ScriptKey = scriptKey,
            ValuesJson = "{}",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.ScriptRuntimeStates.Add(state);

        return state;
    }

    /// <summary>
    /// Собирает globals-объект, через который пользовательский скрипт получает текущее время, теги, state, эмулятор и REST API.
    /// </summary>
    /// <param name="emulator">Эмулятор с конфигурацией всех тегов.</param>
    /// <param name="tag">Тег, для которого выполняется скрипт.</param>
    /// <param name="timestamp">Время расчета значения.</param>
    /// <param name="stateValues">Десериализованный persistent state текущего скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операций, доступных из REST-контекста.</param>
    /// <param name="currentValue">Текущее значение формулы, переданное в formula-script, или <see langword="null"/>.</param>
    /// <returns>Globals-объект для запуска Roslyn-скрипта.</returns>
    private TagScriptGlobals BuildGlobals(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        Dictionary<string, object?> stateValues,
        CancellationToken cancellationToken,
        object? currentValue)
    {
        var values = emulator.Tags
            .Select(t =>
            {
                var tagType = UniEmuJson.EnumValue<TagType>(t.Type);
                var scriptTagType = ToScriptValueType(tagType);
                if (t.Id == tag.Id && currentValue is not null)
                    return new TagScriptValue(t.Key, t.Name, currentValue, scriptTagType, timestamp);

                if (stateStore.TryGet(emulator.Id, t.Id, out var runtimeValue))
                    return new TagScriptValue(t.Key, t.Name, runtimeValue.Value, scriptTagType, runtimeValue.Timestamp);

                return new TagScriptValue(t.Key, t.Name, ConvertPreview(t), scriptTagType, timestamp);
            })
            .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.Timestamp).First(), StringComparer.OrdinalIgnoreCase);

        stateStore.TryGet(emulator.Id, tag.Id, out var previous);
        var scriptNow = ApplicationGlobalization.ToApplicationTime(timestamp);

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var tagValue = currentValue ?? previous?.Value;
        var tagTimestamp = currentValue is null ? previous?.Timestamp : timestamp;
        return new TagScriptGlobals(
            scriptNow,
            new TagScriptValue(tag.Key, tag.Name, tagValue, ToScriptValueType(tagType), tagTimestamp),
            new TagScriptTagAccessor(
                values,
                (tagName, value) => SetStaticTag(emulator, tagName, value, timestamp)),
            new TagScriptEmulatorContext(emulator.Id, emulator.Name, emulator.Status, emulator.StartedAt),
            new TagScriptStateContext(
                emulator.Status == nameof(EmulatorStatus.Running),
                previous?.Value,
                previous?.NumericValue,
                previous?.Timestamp,
                ToScriptStateValues(stateValues)
            ),
            restOperations is null
                ? TagScriptRestContext.CreateDisabled(cancellationToken)
                : new TagScriptRestContext(restOperations, cancellationToken)
        );
    }

    /// <summary>
    /// Преобразует raw persistent state в типизированные значения, доступные через скриптовый API.
    /// </summary>
    /// <param name="stateValues">Десериализованный словарь значений state.</param>
    /// <returns>Словарь значений state в формате <see cref="TagScriptValue"/>.</returns>
    private static Dictionary<string, TagScriptValue> ToScriptStateValues(Dictionary<string, object?> stateValues)
    {
        return stateValues.ToDictionary(
            value => value.Key,
            value => new TagScriptValue(value.Key, value.Key, value.Value, ToScriptValueType(value.Value), null),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Определяет скриптовый тип значения по фактическому CLR-значению.
    /// </summary>
    /// <param name="value">Значение тега или state.</param>
    /// <returns>Тип значения для публичного скриптового API.</returns>
    private static TagScriptValueType ToScriptValueType(object? value) => value switch
    {
        bool => TagScriptValueType.Bool,
        byte or short or int or long => TagScriptValueType.Int,
        float or double or decimal => TagScriptValueType.Double,
        _ => TagScriptValueType.String,
    };

    /// <summary>
    /// Преобразует доменный тип тега в тип значения, отдаваемый скриптовому API.
    /// </summary>
    /// <param name="type">Тип тега из конфигурации UniEmu.</param>
    /// <returns>Тип значения для публичного скриптового API.</returns>
    private static TagScriptValueType ToScriptValueType(TagType type) => type switch
    {
        TagType.Bool => TagScriptValueType.Bool,
        TagType.Int => TagScriptValueType.Int,
        TagType.Double => TagScriptValueType.Double,
        TagType.String => TagScriptValueType.String,
        _ => TagScriptValueType.String,
    };

    /// <summary>
    /// Обновляет static-тег из пользовательского скрипта, синхронизируя preview, runtime state и отложенную запись в базу.
    /// </summary>
    /// <param name="emulator">Эмулятор, в котором ищется изменяемый тег.</param>
    /// <param name="tagName">Имя или ключ static-тега.</param>
    /// <param name="value">Новое значение, переданное из скрипта.</param>
    /// <param name="timestamp">Время изменения значения.</param>
    /// <returns>Типизированное и округленное значение, фактически записанное в тег.</returns>
    private object? SetStaticTag(EmulatorEntity emulator, string tagName, object? value, DateTimeOffset timestamp)
    {
        var tag = emulator.Tags.FirstOrDefault(t =>
            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) ||
            t.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase)
        );

        if (tag is null)
            throw new InvalidOperationException($"Tag '{tagName}' was not found.");

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source != TagSource.Static)
            throw new InvalidOperationException($"Tag '{tagName}' is not static.");

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var typedValue = TelemetryValueGenerator.ApplyTagRounding(tagType, tag, CastResult(tagType, value, tag.Preview));

        tag.Preview = TelemetryValueGenerator.ToPreview(typedValue);
        stateStore.Set(emulator.Id, tag.Id, tag.Name, typedValue, TelemetryValueGenerator.ToNumericValue(typedValue), timestamp);
        previewFlushService?.MarkDirty(emulator.Id, tag.Id, tag.Preview);

        return typedValue;
    }

    /// <summary>
    /// Приводит результат выполнения скрипта к типу тега, используя preview как fallback для пустого или некорректного результата.
    /// </summary>
    /// <param name="tagType">Ожидаемый тип тега.</param>
    /// <param name="result">Значение, возвращенное пользовательским скриптом.</param>
    /// <param name="preview">Последнее preview-значение тега для fallback-преобразований.</param>
    /// <returns>Значение, приведенное к доменному типу тега.</returns>
    private static object? CastResult(TagType tagType, object? result, string preview)
    {
        if (result is null)
            return tagType == TagType.String ? preview : CastPreview(tagType, preview);

        return tagType switch
        {
            TagType.Bool => ToBool(result, preview),
            TagType.Int => (int)Math.Round(ToDouble(result, preview)),
            TagType.Double => ToDouble(result, preview),
            TagType.String => TelemetryValueGenerator.ToPreview(result),
            _ => result,
        };
    }

    /// <summary>
    /// Преобразует строковое preview-значение в CLR-значение указанного типа тега.
    /// </summary>
    /// <param name="tagType">Тип тега, к которому приводится preview.</param>
    /// <param name="preview">Строковое preview-значение из базы или формы.</param>
    /// <returns>Типизированное значение preview.</returns>
    private static object? CastPreview(TagType tagType, string preview) => tagType switch
    {
        TagType.Bool => ToBool(preview, "false"),
        TagType.Int => (int)Math.Round(ToDouble(preview, "0")),
        TagType.Double => ToDouble(preview, "0"),
        TagType.String => preview,
        _ => null,
    };

    /// <summary>
    /// Возвращает типизированное значение тега из его сохраненного preview.
    /// </summary>
    /// <param name="tag">Тег, из которого берутся тип и preview.</param>
    /// <returns>Preview, приведенное к типу тега.</returns>
    private static object? ConvertPreview(EmulatorTagEntity tag)
    {
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        return CastPreview(tagType, tag.Preview);
    }

    /// <summary>
    /// Приводит значение к булевому типу, интерпретируя числа и числовые строки как <c>false</c> для нуля и <c>true</c> иначе.
    /// </summary>
    /// <param name="value">Исходное значение скрипта или preview.</param>
    /// <param name="preview">Fallback preview для случаев, когда исходное значение нельзя разобрать напрямую.</param>
    /// <returns>Булево представление значения.</returns>
    private static bool ToBool(object value, string preview) => value switch
    {
        bool boolValue => boolValue,
        string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
        string stringValue => ToDouble(stringValue, preview) != 0,
        _ => ToDouble(value, preview) != 0,
    };

    /// <summary>
    /// Приводит значение к <see cref="double"/> с invariant culture и fallback на preview при нечисловой строке.
    /// </summary>
    /// <param name="value">Исходное значение скрипта или preview.</param>
    /// <param name="preview">Fallback preview для случаев, когда исходное значение нельзя разобрать напрямую.</param>
    /// <returns>Числовое представление значения или ноль, если разобрать не удалось.</returns>
    private static double ToDouble(object value, string preview) => value switch
    {
        byte byteValue => byteValue,
        short shortValue => shortValue,
        int intValue => intValue,
        long longValue => longValue,
        float floatValue => floatValue,
        double doubleValue => doubleValue,
        decimal decimalValue => (double)decimalValue,
        bool boolValue => boolValue ? 1 : 0,
        string stringValue => double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : (double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0),
        IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
        _ => double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out var fallback) ? fallback : 0,
    };

    /// <summary>
    /// Описывает входной скрипт тега и ключ, под которым хранится его persistent state.
    /// </summary>
    /// <param name="Path">Нормализованный путь скрипта для Roslyn и <c>#load</c>.</param>
    /// <param name="Content">Исходное содержимое входного скрипта.</param>
    /// <param name="StateKey">Стабильный ключ persistent state для этого скрипта.</param>
    private sealed record ScriptContent(string Path, string Content, string StateKey);
}
