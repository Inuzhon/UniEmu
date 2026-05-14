using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Services;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxLanguageService
{
    private static readonly CsxScriptEnvironment s_defaultEnvironment = new();

    private readonly CsxDiagnosticsService diagnostics;
    private readonly CsxCompletionService completion;
    private readonly CsxHoverService hover;
    private readonly CsxSignatureHelpService signatureHelp;
    private readonly CsxNavigationService navigation;
    private readonly CsxRenameService rename;
    private readonly CsxFormattingService formatting;
    private readonly CsxFoldingService folding;
    private readonly CsxSemanticTokensService semanticTokens;
    private readonly CsxCallHierarchyService callHierarchy;

    public CsxLanguageService()
        : this(CreateDefaultContextFactory())
    {
    }

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

    internal static int MetadataReferenceCacheCount => s_defaultEnvironment.MetadataReferenceCacheCount;

    internal static void ClearMetadataReferenceCacheForTests()
    {
        s_defaultEnvironment.ClearMetadataReferenceCacheForTests();
    }

    internal static IReadOnlyList<Microsoft.CodeAnalysis.MetadataReference> CreateMetadataReferencesForTests(Type globalsType)
    {
        return s_defaultEnvironment.CreateMetadataReferences(globalsType);
    }

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

    public Task<IReadOnlyList<CsxTextEdit>> FormatDocumentAsync(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null,
        CancellationToken cancellationToken = default)
    {
        return formatting.FormatDocumentAsync(content, cancellationToken);
    }

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
public sealed record CsxDiagnostic(
    string Code,
    string Message,
    CsxDiagnosticSeverity Severity,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

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
public sealed record CsxLocation(
    string DocumentPath,
    CsxTextRange Range);

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
