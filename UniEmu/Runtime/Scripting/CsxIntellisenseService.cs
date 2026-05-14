using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting;

/// <summary>
/// Подготавливает языковые возможности CSX-редактора с учетом сохраненных скриптов UniEmu.
/// </summary>
public sealed class CsxIntellisenseService(
    UniEmuDbContext db,
    CsxLanguageService language)
{
    /// <summary>
    /// Возвращает диагностические сообщения для CSX-документа.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и контекстом документа.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список диагностик.</returns>
    public async Task<IReadOnlyList<CsxDiagnostic>> GetDiagnosticsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return (await language.AnalyzeAsync(
                EntryPath(context),
                sourceCode,
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken))
            .Diagnostics;
    }

    /// <summary>
    /// Возвращает варианты автодополнения для позиции в CSX-документе.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список элементов автодополнения.</returns>
    public async Task<IReadOnlyList<CsxCompletionItem>> GetCompletionsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.GetCompletionsAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает hover-информацию для символа под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Hover-информация или <see langword="null"/>, если символ не найден.</returns>
    public async Task<CsxHover?> GetHoverAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.GetHoverAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает подсказку сигнатуры для текущего вызова.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Подсказка сигнатуры или <see langword="null"/>, если она недоступна.</returns>
    public async Task<CsxSignatureHelp?> GetSignatureHelpAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.GetSignatureHelpAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает определения символа под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список мест определения.</returns>
    public async Task<IReadOnlyList<CsxLocation>> GetDefinitionsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetDefinitionsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri);
    }

    /// <summary>
    /// Возвращает определения типа символа под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список мест определения типа.</returns>
    public async Task<IReadOnlyList<CsxLocation>> GetTypeDefinitionsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetTypeDefinitionsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri);
    }

    /// <summary>
    /// Возвращает ссылки на символ под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список мест использования символа.</returns>
    public async Task<IReadOnlyList<CsxLocation>> GetReferencesAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetReferencesAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                request.IncludeDeclaration,
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri);
    }

    /// <summary>
    /// Возвращает реализации символа под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список мест реализации.</returns>
    public async Task<IReadOnlyList<CsxLocation>> GetImplementationsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetImplementationsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri);
    }

    /// <summary>
    /// Подготавливает набор правок для переименования символа.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом, позицией и новым именем.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Набор правок или <see langword="null"/>, если переименование недоступно.</returns>
    public async Task<CsxWorkspaceEdit?> RenameAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return null;
        }

        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);
        var edit = await language.RenameAsync(
            entryPath,
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            request.NewName,
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);

        return edit is null
            ? null
            : new CsxWorkspaceEdit(edit.DocumentEdits
                .Select(documentEdit => documentEdit with
                {
                    DocumentPath = MapDocumentPath(documentEdit.DocumentPath, entryPath, request.DocumentUri),
                })
                .ToArray());
    }

    /// <summary>
    /// Форматирует весь CSX-документ.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    public async Task<IReadOnlyList<CsxTextEdit>> FormatDocumentAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.FormatDocumentAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Форматирует диапазон CSX-документа.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и диапазоном.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    public async Task<IReadOnlyList<CsxTextEdit>> FormatRangeAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.FormatRangeAsync(
            EntryPath(context),
            sourceCode,
            request.Range ?? new CsxTextRange(0, 0, 0, 0),
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает диапазоны сворачивания CSX-документа.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список диапазонов сворачивания.</returns>
    public async Task<IReadOnlyList<CsxFoldingRange>> GetFoldingRangesAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.GetFoldingRangesAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Возвращает semantic tokens для подсветки CSX-документа.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Semantic tokens и легенда токенов.</returns>
    public async Task<CsxSemanticTokens> GetSemanticTokensAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);

        return await language.GetSemanticTokensAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts,
            typeof(TagScriptGlobals),
            cancellationToken);
    }

    /// <summary>
    /// Подготавливает элементы call hierarchy для символа под курсором.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список элементов call hierarchy.</returns>
    public async Task<IReadOnlyList<CsxCallHierarchyItem>> PrepareCallHierarchyAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return MapCallHierarchyItems(
            await language.PrepareCallHierarchyAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri);
    }

    /// <summary>
    /// Возвращает входящие вызовы для элемента call hierarchy.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией элемента.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список входящих вызовов.</returns>
    public async Task<IReadOnlyList<CsxCallHierarchyIncomingCall>> GetIncomingCallsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return (await language.GetIncomingCallsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken))
            .Select(call => call with { From = MapCallHierarchyItem(call.From, entryPath, request.DocumentUri) })
            .ToArray();
    }

    /// <summary>
    /// Возвращает исходящие вызовы для элемента call hierarchy.
    /// </summary>
    /// <param name="request">Запрос редактора с исходным кодом и позицией элемента.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список исходящих вызовов.</returns>
    public async Task<IReadOnlyList<CsxCallHierarchyOutgoingCall>> GetOutgoingCallsAsync(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        var context = CsxDocumentContextParser.Parse(request.DocumentUri);
        var sourceCode = request.SourceCode ?? string.Empty;
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, cancellationToken);
        var entryPath = EntryPath(context);

        return (await language.GetOutgoingCallsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts,
                typeof(TagScriptGlobals),
                cancellationToken))
            .Select(call => call with { To = MapCallHierarchyItem(call.To, entryPath, request.DocumentUri) })
            .ToArray();
    }

    private async Task<Dictionary<string, string>> LoadVisibleScriptsAsync(
        CsxDocumentContext context,
        string sourceCode,
        CancellationToken cancellationToken)
    {
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

        if (!string.IsNullOrWhiteSpace(context.ScriptName))
        {
            result[TagScriptPath.Normalize(context.ScriptName)] = sourceCode;
        }

        return result;
    }

    private static string EntryPath(CsxDocumentContext context)
    {
        return TagScriptPath.Normalize(string.IsNullOrWhiteSpace(context.ScriptName)
            ? "script.csx"
            : context.ScriptName);
    }

    private static IReadOnlyList<CsxLocation> MapLocations(
        IReadOnlyList<CsxLocation> locations,
        string entryPath,
        string? documentUri)
    {
        return locations
            .Select(location => location with
            {
                DocumentPath = MapDocumentPath(location.DocumentPath, entryPath, documentUri),
            })
            .ToArray();
    }

    private static IReadOnlyList<CsxCallHierarchyItem> MapCallHierarchyItems(
        IReadOnlyList<CsxCallHierarchyItem> items,
        string entryPath,
        string? documentUri)
    {
        return items.Select(item => MapCallHierarchyItem(item, entryPath, documentUri)).ToArray();
    }

    private static CsxCallHierarchyItem MapCallHierarchyItem(
        CsxCallHierarchyItem item,
        string entryPath,
        string? documentUri)
    {
        return item with
        {
            DocumentPath = MapDocumentPath(item.DocumentPath, entryPath, documentUri),
        };
    }

    private static string MapDocumentPath(string documentPath, string entryPath, string? documentUri)
    {
        return string.Equals(documentPath, entryPath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(documentUri)
            ? documentUri
            : documentPath;
    }

}

/// <summary>
/// Запрос CSX-редактора к backend-сервисам IntelliSense.
/// </summary>
/// <param name="SourceCode">Текущий исходный код документа.</param>
/// <param name="DocumentUri">URI или путь документа в редакторе.</param>
/// <param name="Position">Позиция курсора в документе.</param>
/// <param name="Range">Диапазон документа для операций форматирования.</param>
/// <param name="NewName">Новое имя для операции переименования.</param>
/// <param name="IncludeDeclaration">Признак включения объявления в список ссылок.</param>
public sealed record CsxIntellisenseRequest(
    string? SourceCode,
    string? DocumentUri,
    CsxEditorPosition? Position,
    CsxTextRange? Range = null,
    string? NewName = null,
    bool IncludeDeclaration = true);

/// <summary>
/// Позиция в текстовом документе редактора.
/// </summary>
/// <param name="Line">Номер строки, начиная с 1.</param>
/// <param name="Column">Номер колонки, начиная с 1.</param>
public sealed record CsxEditorPosition(int Line, int Column);
