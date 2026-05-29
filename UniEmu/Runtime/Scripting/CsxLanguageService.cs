using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Services;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting;

/// <summary>
/// Фасад language features для CSX-редактора: диагностика, completion, hover, навигация, форматирование и semantic tokens.
/// </summary>
public sealed class CsxLanguageService
{
    /// <summary>
    /// Общее окружение Roslyn, переиспользуемое между экземплярами сервиса для кэша metadata reference.
    /// </summary>
    private static readonly CsxScriptEnvironment s_defaultEnvironment = new();

    /// <summary>
    /// Сервис компиляционных и security-диагностик CSX-документа.
    /// </summary>
    private readonly CsxDiagnosticsService diagnostics;
    /// <summary>
    /// Сервис автодополнения CSX-документа.
    /// </summary>
    private readonly CsxCompletionService completion;
    /// <summary>
    /// Сервис hover-информации по символам CSX-документа.
    /// </summary>
    private readonly CsxHoverService hover;
    /// <summary>
    /// Сервис подсказок сигнатур методов и конструкторов.
    /// </summary>
    private readonly CsxSignatureHelpService signatureHelp;
    /// <summary>
    /// Сервис переходов к определениям, type definitions, references и implementations.
    /// </summary>
    private readonly CsxNavigationService navigation;
    /// <summary>
    /// Сервис rename-операций по текущему CSX-документу.
    /// </summary>
    private readonly CsxRenameService rename;
    /// <summary>
    /// Сервис форматирования всего документа или выбранного диапазона.
    /// </summary>
    private readonly CsxFormattingService formatting;
    /// <summary>
    /// Сервис диапазонов сворачивания кода.
    /// </summary>
    private readonly CsxFoldingService folding;
    /// <summary>
    /// Сервис semantic tokens для подсветки редактора.
    /// </summary>
    private readonly CsxSemanticTokensService semanticTokens;
    /// <summary>
    /// Сервис call hierarchy для функций и методов скрипта.
    /// </summary>
    private readonly CsxCallHierarchyService callHierarchy;

    /// <summary>
    /// Создает language service с окружением и Roslyn context factory по умолчанию.
    /// </summary>
    public CsxLanguageService()
        : this(CreateDefaultContextFactory())
    {
    }

    /// <summary>
    /// Создает language service с указанной фабрикой Roslyn-контекста и стандартными feature-сервисами.
    /// </summary>
    /// <param name="contextFactory">Фабрика Roslyn workspace для CSX-документов.</param>
    private CsxLanguageService(CsxRoslynContextFactory contextFactory)
        : this(
            new CsxDiagnosticsService(s_defaultEnvironment),
            new CsxCompletionService(contextFactory),
            new CsxHoverService(contextFactory),
            new CsxSignatureHelpService(contextFactory),
            new CsxNavigationService(contextFactory),
            new CsxRenameService(contextFactory),
            new CsxFormattingService(),
            new CsxFoldingService(contextFactory),
            new CsxSemanticTokensService(contextFactory),
            new CsxCallHierarchyService(contextFactory))
    {
    }

    /// <summary>
    /// Создает language service с явно заданными feature-сервисами.
    /// </summary>
    /// <param name="diagnostics">Сервис диагностики CSX-документа.</param>
    /// <param name="completion">Сервис автодополнения.</param>
    /// <param name="hover">Сервис hover-информации.</param>
    /// <param name="signatureHelp">Сервис подсказок сигнатур.</param>
    /// <param name="navigation">Сервис навигации по символам.</param>
    /// <param name="rename">Сервис переименования символов.</param>
    /// <param name="formatting">Сервис форматирования.</param>
    /// <param name="folding">Сервис диапазонов сворачивания.</param>
    /// <param name="semanticTokens">Сервис semantic tokens.</param>
    /// <param name="callHierarchy">Сервис call hierarchy.</param>
    public CsxLanguageService(
        CsxDiagnosticsService diagnostics,
        CsxCompletionService completion,
        CsxHoverService hover,
        CsxSignatureHelpService signatureHelp,
        CsxNavigationService navigation,
        CsxRenameService rename,
        CsxFormattingService formatting,
        CsxFoldingService folding,
        CsxSemanticTokensService semanticTokens,
        CsxCallHierarchyService callHierarchy)
    {
        this.diagnostics = diagnostics;
        this.completion = completion;
        this.hover = hover;
        this.signatureHelp = signatureHelp;
        this.navigation = navigation;
        this.rename = rename;
        this.formatting = formatting;
        this.folding = folding;
        this.semanticTokens = semanticTokens;
        this.callHierarchy = callHierarchy;
    }

    /// <summary>
    /// Возвращает количество закэшированных наборов metadata reference в окружении по умолчанию.
    /// </summary>
    internal static int MetadataReferenceCacheCount => s_defaultEnvironment.MetadataReferenceCacheCount;

    /// <summary>
    /// Очищает кэш metadata reference окружения по умолчанию для изолированных тестов.
    /// </summary>
    internal static void ClearMetadataReferenceCacheForTests()
    {
        s_defaultEnvironment.ClearMetadataReferenceCacheForTests();
    }

    /// <summary>
    /// Создает набор metadata reference окружения по умолчанию для проверок в тестах.
    /// </summary>
    /// <param name="globalsType">Тип globals-объекта, сборка которого должна быть доступна скрипту.</param>
    /// <returns>Список metadata reference.</returns>
    internal static IReadOnlyList<Microsoft.CodeAnalysis.MetadataReference> CreateMetadataReferencesForTests(Type globalsType)
    {
        return s_defaultEnvironment.CreateMetadataReferences(globalsType);
    }

    /// <summary>
    /// Анализирует CSX-документ без проверки ожидаемого типа возвращаемого значения.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены анализа.</param>
    /// <returns>Результат анализа с диагностическими сообщениями.</returns>
    public async Task<CsxAnalysisResult> AnalyzeAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return await AnalyzeAsync(
            entryPath,
            content,
            visibleScripts,
            globalsType,
            expectedReturnType: null,
            cancellationToken);
    }

    /// <summary>
    /// Анализирует CSX-документ с учетом ожидаемого типа возвращаемого значения.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="expectedReturnType">Ожидаемый тип результата скрипта.</param>
    /// <param name="cancellationToken">Токен отмены анализа.</param>
    /// <returns>Результат анализа с диагностическими сообщениями.</returns>
    public async Task<CsxAnalysisResult> AnalyzeAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType,
        Type? expectedReturnType,
        CancellationToken cancellationToken = default)
    {
        return new CsxAnalysisResult(
            entryPath,
            await diagnostics.AnalyzeAsync(
                TagScriptPath.Normalize(entryPath),
                content,
                visibleScripts,
                globalsType ?? typeof(object),
                expectedReturnType,
                cancellationToken));
    }

    /// <summary>
    /// Возвращает элементы автодополнения для позиции в CSX-документе.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset курсора в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список элементов автодополнения.</returns>
    public Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return completion.GetCompletionsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает hover-информацию для символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Hover-информация или <see langword="null"/>, если символ не найден.</returns>
    public Task<CsxHover?> GetHoverAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return hover.GetHoverAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает подсказки сигнатур для вызова метода или конструктора.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Подсказки сигнатур или <see langword="null"/>.</returns>
    public Task<CsxSignatureHelp?> GetSignatureHelpAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return signatureHelp.GetSignatureHelpAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает расположения определений символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список расположений определений.</returns>
    public Task<IReadOnlyList<CsxLocation>> GetDefinitionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetDefinitionsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает расположения определений типов для символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список расположений определений типов.</returns>
    public Task<IReadOnlyList<CsxLocation>> GetTypeDefinitionsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetTypeDefinitionsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает ссылки на символ под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="includeDeclaration">Включать ли объявление символа в результат.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список расположений ссылок на символ.</returns>
    public Task<IReadOnlyList<CsxLocation>> GetReferencesAsync(
        string entryPath,
        string content,
        int position,
        bool includeDeclaration,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetReferencesAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            includeDeclaration,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает имя и исходные объявления символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Целевой символ для поиска ссылок или <see langword="null"/>.</returns>
    public Task<CsxSymbolReferenceTarget?> GetReferenceTargetAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetReferenceTargetAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает ссылки в указанном CSX-документе, которые резолвятся в заданные объявления символа.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="symbolName">Имя искомого символа.</param>
    /// <param name="declarationLocations">Расположения объявлений искомого символа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список ссылок на символ во входном документе.</returns>
    public Task<IReadOnlyList<CsxLocation>> GetReferencesToTargetAsync(
        string entryPath,
        string content,
        string symbolName,
        IReadOnlyList<CsxLocation> declarationLocations,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetReferencesToTargetAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            symbolName,
            declarationLocations,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает расположения реализаций символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список расположений реализаций.</returns>
    public Task<IReadOnlyList<CsxLocation>> GetImplementationsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return navigation.GetImplementationsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Подготавливает правки для переименования символа в текущем CSX-документе.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="newName">Новое имя символа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Набор правок workspace или <see langword="null"/>, если символ нельзя переименовать.</returns>
    public Task<CsxWorkspaceEdit?> RenameAsync(
        string entryPath,
        string content,
        int position,
        string newName,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return rename.RenameAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            newName,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Форматирует весь CSX-документ.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    public Task<IReadOnlyList<CsxTextEdit>> FormatDocumentAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return formatting.FormatDocumentAsync(content, cancellationToken);
    }

    /// <summary>
    /// Форматирует выбранный диапазон CSX-документа.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="range">Диапазон форматирования.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    public Task<IReadOnlyList<CsxTextEdit>> FormatRangeAsync(
        string entryPath,
        string content,
        CsxTextRange range,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return formatting.FormatRangeAsync(content, range, cancellationToken);
    }

    /// <summary>
    /// Возвращает диапазоны сворачивания для CSX-документа.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список диапазонов сворачивания.</returns>
    public Task<IReadOnlyList<CsxFoldingRange>> GetFoldingRangesAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return folding.GetFoldingRangesAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает semantic tokens для подсветки CSX-документа.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Semantic tokens и легенда токенов.</returns>
    public Task<CsxSemanticTokens> GetSemanticTokensAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return semanticTokens.GetSemanticTokensAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Подготавливает элементы call hierarchy для символа под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список элементов call hierarchy.</returns>
    public Task<IReadOnlyList<CsxCallHierarchyItem>> PrepareCallHierarchyAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return callHierarchy.PrepareAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает входящие вызовы для элемента call hierarchy под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список входящих вызовов.</returns>
    public Task<IReadOnlyList<CsxCallHierarchyIncomingCall>> GetIncomingCallsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return callHierarchy.GetIncomingCallsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает исходящие вызовы для элемента call hierarchy под указанной позицией.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст документа.</param>
    /// <param name="position">Offset позиции в документе.</param>
    /// <param name="visibleScripts">Скрипты, доступные для <c>#load</c>.</param>
    /// <param name="globalsType">Тип globals-объекта скрипта.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список исходящих вызовов.</returns>
    public Task<IReadOnlyList<CsxCallHierarchyOutgoingCall>> GetOutgoingCallsAsync(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return callHierarchy.GetOutgoingCallsAsync(
            TagScriptPath.Normalize(entryPath),
            content,
            position,
            visibleScripts,
            globalsType ?? typeof(object),
            cancellationToken);
    }

    /// <summary>
    /// Создает фабрику Roslyn-контекста для стандартного окружения CSX.
    /// </summary>
    /// <returns>Фабрика контекстов для language features.</returns>
    private static CsxRoslynContextFactory CreateDefaultContextFactory()
    {
        return new CsxRoslynContextFactory(s_defaultEnvironment, new CsxLoadedScriptExpander());
    }
}

/// <summary>
/// Результат анализа CSX-документа.
/// </summary>
/// <param name="EntryPath">Нормализованный путь входного CSX-документа.</param>
/// <param name="Diagnostics">Диагностические сообщения анализа.</param>
public sealed record CsxAnalysisResult(
    string EntryPath,
    IReadOnlyList<CsxDiagnostic> Diagnostics);

/// <summary>
/// Диагностическое сообщение CSX-языкового сервиса.
/// </summary>
/// <param name="Code">Код диагностики.</param>
/// <param name="Message">Текст диагностики.</param>
/// <param name="Severity">Важность диагностики.</param>
/// <param name="StartLine">Начальная строка диапазона.</param>
/// <param name="StartCharacter">Начальная колонка диапазона.</param>
/// <param name="EndLine">Конечная строка диапазона.</param>
/// <param name="EndCharacter">Конечная колонка диапазона.</param>
/// <param name="DocumentPath">Document path or URI that owns this diagnostic.</param>
public sealed record CsxDiagnostic(
    string Code,
    string Message,
    CsxDiagnosticSeverity Severity,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter,
    string? DocumentPath = null);

/// <summary>
/// Важность диагностического сообщения CSX-языкового сервиса.
/// </summary>
public enum CsxDiagnosticSeverity
{
    /// <summary>Ошибка, блокирующая корректное выполнение кода.</summary>
    Error = 1,

    /// <summary>Предупреждение о потенциальной проблеме.</summary>
    Warning = 2,

    /// <summary>Информационное сообщение.</summary>
    Information = 3,

    /// <summary>Подсказка редактора.</summary>
    Hint = 4,
}

/// <summary>
/// Элемент автодополнения CSX-редактора.
/// </summary>
/// <param name="Label">Отображаемая подпись элемента.</param>
/// <param name="SortText">Текст сортировки.</param>
/// <param name="FilterText">Текст фильтрации.</param>
/// <param name="InsertText">Текст, вставляемый в документ.</param>
/// <param name="Detail">Краткие дополнительные сведения.</param>
/// <param name="Documentation">Документация элемента.</param>
/// <param name="Kind">Тип элемента автодополнения.</param>
public sealed record CsxCompletionItem(
    string Label,
    string SortText,
    string FilterText,
    string InsertText,
    string? Detail = null,
    string? Documentation = null,
    string Kind = "text");

/// <summary>
/// Hover-информация для символа CSX-документа.
/// </summary>
/// <param name="Signature">Сигнатура или краткое описание символа.</param>
/// <param name="Documentation">Документация символа.</param>
/// <param name="StartOffset">Начальный offset диапазона символа.</param>
/// <param name="EndOffset">Конечный offset диапазона символа.</param>
public sealed record CsxHover(
    string Signature,
    string? Documentation,
    int StartOffset,
    int EndOffset);

/// <summary>
/// Подсказка сигнатур для вызова метода или конструктора.
/// </summary>
/// <param name="Signatures">Доступные сигнатуры.</param>
/// <param name="ActiveSignature">Индекс активной сигнатуры.</param>
/// <param name="ActiveParameter">Индекс активного параметра.</param>
public sealed record CsxSignatureHelp(
    IReadOnlyList<CsxSignature> Signatures,
    int ActiveSignature,
    int ActiveParameter);

/// <summary>
/// Описание одной сигнатуры вызываемого символа.
/// </summary>
/// <param name="Label">Текст сигнатуры.</param>
/// <param name="Documentation">Документация сигнатуры.</param>
/// <param name="Parameters">Параметры сигнатуры.</param>
public sealed record CsxSignature(
    string Label,
    string? Documentation,
    IReadOnlyList<CsxSignatureParameter> Parameters);

/// <summary>
/// Описание параметра сигнатуры.
/// </summary>
/// <param name="Label">Текст параметра.</param>
/// <param name="Documentation">Документация параметра.</param>
public sealed record CsxSignatureParameter(
    string Label,
    string? Documentation);

/// <summary>
/// Диапазон текста в документе редактора.
/// </summary>
/// <param name="StartLine">Начальная строка диапазона.</param>
/// <param name="StartCharacter">Начальная колонка диапазона.</param>
/// <param name="EndLine">Конечная строка диапазона.</param>
/// <param name="EndCharacter">Конечная колонка диапазона.</param>
public sealed record CsxTextRange(
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

/// <summary>
/// Местоположение диапазона в CSX-документе.
/// </summary>
/// <param name="DocumentPath">Путь или URI документа.</param>
/// <param name="Range">Диапазон внутри документа.</param>
/// <param name="SourceCode">Текст документа, если клиенту нужно создать модель для перехода.</param>
public sealed record CsxLocation(
    string DocumentPath,
    CsxTextRange Range,
    string? SourceCode = null);

/// <summary>
/// Целевой символ для поиска ссылок в других CSX-документах.
/// </summary>
/// <param name="Name">Имя символа, используемое для первичного поиска токенов.</param>
/// <param name="DeclarationLocations">Расположения объявлений символа в исходных CSX-документах.</param>
public sealed record CsxSymbolReferenceTarget(
    string Name,
    IReadOnlyList<CsxLocation> DeclarationLocations);

/// <summary>
/// Текстовая правка документа.
/// </summary>
/// <param name="Range">Диапазон заменяемого текста.</param>
/// <param name="NewText">Новый текст диапазона.</param>
public sealed record CsxTextEdit(
    CsxTextRange Range,
    string NewText);

/// <summary>
/// Набор правок одного документа.
/// </summary>
/// <param name="DocumentPath">Путь или URI документа.</param>
/// <param name="Edits">Правки документа.</param>
public sealed record CsxDocumentEdit(
    string DocumentPath,
    IReadOnlyList<CsxTextEdit> Edits);

/// <summary>
/// Набор правок для нескольких документов рабочего пространства.
/// </summary>
/// <param name="DocumentEdits">Правки документов.</param>
public sealed record CsxWorkspaceEdit(
    IReadOnlyList<CsxDocumentEdit> DocumentEdits);

/// <summary>
/// Диапазон сворачивания в CSX-документе.
/// </summary>
/// <param name="StartLine">Начальная строка диапазона.</param>
/// <param name="EndLine">Конечная строка диапазона.</param>
/// <param name="Kind">Тип сворачиваемого диапазона.</param>
public sealed record CsxFoldingRange(
    int StartLine,
    int EndLine,
    string? Kind = null);

/// <summary>
/// Легенда semantic tokens для редактора.
/// </summary>
/// <param name="TokenTypes">Типы токенов.</param>
/// <param name="TokenModifiers">Модификаторы токенов.</param>
public sealed record CsxSemanticTokensLegend(
    IReadOnlyList<string> TokenTypes,
    IReadOnlyList<string> TokenModifiers);

/// <summary>
/// Semantic tokens CSX-документа.
/// </summary>
/// <param name="Legend">Легенда токенов.</param>
/// <param name="Data">Закодированный поток токенов.</param>
public sealed record CsxSemanticTokens(
    CsxSemanticTokensLegend Legend,
    IReadOnlyList<int> Data);

/// <summary>
/// Элемент call hierarchy.
/// </summary>
/// <param name="Name">Имя символа.</param>
/// <param name="Kind">Тип символа.</param>
/// <param name="Detail">Дополнительные сведения о символе.</param>
/// <param name="DocumentPath">Путь или URI документа.</param>
/// <param name="Range">Полный диапазон символа.</param>
/// <param name="SelectionRange">Диапазон имени символа.</param>
public sealed record CsxCallHierarchyItem(
    string Name,
    string Kind,
    string? Detail,
    string DocumentPath,
    CsxTextRange Range,
    CsxTextRange SelectionRange);

/// <summary>
/// Входящий вызов call hierarchy.
/// </summary>
/// <param name="From">Символ, из которого выполняется вызов.</param>
/// <param name="FromRanges">Диапазоны вызова в исходном символе.</param>
public sealed record CsxCallHierarchyIncomingCall(
    CsxCallHierarchyItem From,
    IReadOnlyList<CsxTextRange> FromRanges);

/// <summary>
/// Исходящий вызов call hierarchy.
/// </summary>
/// <param name="To">Символ, в который выполняется вызов.</param>
/// <param name="FromRanges">Диапазоны вызова в текущем символе.</param>
public sealed record CsxCallHierarchyOutgoingCall(
    CsxCallHierarchyItem To,
    IReadOnlyList<CsxTextRange> FromRanges);
