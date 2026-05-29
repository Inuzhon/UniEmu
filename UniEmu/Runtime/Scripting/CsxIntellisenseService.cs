using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime.Scripting.Common;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting;

/// <summary>
/// Подготавливает языковые возможности CSX-редактора с учетом сохраненных скриптов UniEmu.
/// </summary>
public sealed class CsxIntellisenseService(
    UniEmuDbContext db,
    CsxLanguageService language)
{
    private static readonly CsxLoadedScriptExpander s_expander = new();

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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        var result = await language.AnalyzeAsync(
                entryPath,
                sourceCode,
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken);

        return MapDiagnostics(result.Diagnostics, entryPath, request.DocumentUri);
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.GetCompletionsAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.GetHoverAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.GetSignatureHelpAsync(
            EntryPath(context),
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetDefinitionsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri,
            sourceCode,
            visibleScripts);
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetTypeDefinitionsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri,
            sourceCode,
            visibleScripts);
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);
        var position = CsxPositionMapper.ToOffset(sourceCode, request.Position);
        var referenceTarget = await language.GetReferenceTargetAsync(
            entryPath,
            sourceCode,
            position,
            visibleScripts.ContentByPath,
            typeof(TagScriptGlobals),
            cancellationToken);

        var locations = MapLocations(
            await language.GetReferencesAsync(
                entryPath,
                sourceCode,
                position,
                request.IncludeDeclaration,
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri,
            sourceCode,
            visibleScripts).ToList();

        if (referenceTarget is not null)
        {
            locations.AddRange(await GetReferencesFromDependentScriptsAsync(
                context,
                entryPath,
                sourceCode,
                request.DocumentUri,
                referenceTarget,
                cancellationToken));
        }

        return locations
            .Distinct()
            .OrderBy(location => location.DocumentPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.Range.StartLine)
            .ThenBy(location => location.Range.StartCharacter)
            .ToArray();
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return MapLocations(
            await language.GetImplementationsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken),
            entryPath,
            request.DocumentUri,
            sourceCode,
            visibleScripts);
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);
        var edit = await language.RenameAsync(
            entryPath,
            sourceCode,
            CsxPositionMapper.ToOffset(sourceCode, request.Position),
            request.NewName,
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.FormatDocumentAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.FormatRangeAsync(
            EntryPath(context),
            sourceCode,
            request.Range ?? new CsxTextRange(0, 0, 0, 0),
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.GetFoldingRangesAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);

        return await language.GetSemanticTokensAsync(
            EntryPath(context),
            sourceCode,
            visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return MapCallHierarchyItems(
            await language.PrepareCallHierarchyAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return (await language.GetIncomingCallsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
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
        var visibleScripts = await LoadVisibleScriptsAsync(context, sourceCode, request.DocumentUri, cancellationToken);
        var entryPath = EntryPath(context);

        return (await language.GetOutgoingCallsAsync(
                entryPath,
                sourceCode,
                CsxPositionMapper.ToOffset(sourceCode, request.Position),
                visibleScripts.ContentByPath,
                typeof(TagScriptGlobals),
                cancellationToken))
            .Select(call => call with { To = MapCallHierarchyItem(call.To, entryPath, request.DocumentUri) })
            .ToArray();
    }

    private async Task<VisibleScriptSet> LoadVisibleScriptsAsync(
        CsxDocumentContext context,
        string sourceCode,
        string? documentUri,
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

        var contentByPath = VisibleScriptResolver.ToContentMap(scripts);
        var documentsByPath = CreateVisibleDocumentMap(scripts);

        if (!string.IsNullOrWhiteSpace(context.ScriptName))
        {
            VisibleScriptResolver.AddOrReplace(contentByPath, context.ScriptName, sourceCode);
            if (!string.IsNullOrWhiteSpace(documentUri))
            {
                documentsByPath[TagScriptPath.Normalize(context.ScriptName)] = new VisibleScriptDocument(documentUri, sourceCode);
            }
        }

        return new VisibleScriptSet(contentByPath, documentsByPath);
    }

    /// <summary>
    /// Ищет ссылки в сохраненных скриптах, которые загружают текущий документ через <c>#load</c>.
    /// </summary>
    /// <param name="context">Контекст текущего CSX-документа.</param>
    /// <param name="entryPath">Нормализованный путь текущего документа.</param>
    /// <param name="sourceCode">Текущий текст документа из редактора.</param>
    /// <param name="documentUri">URI текущего документа в редакторе.</param>
    /// <param name="referenceTarget">Целевой символ, для которого ищутся ссылки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ссылки из зависимых скриптов, уже сопоставленные с URI Monaco-документов.</returns>
    private async Task<IReadOnlyList<CsxLocation>> GetReferencesFromDependentScriptsAsync(
        CsxDocumentContext context,
        string entryPath,
        string sourceCode,
        string? documentUri,
        CsxSymbolReferenceTarget referenceTarget,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.ScriptName))
        {
            return [];
        }

        var scripts = await LoadReferenceSearchScriptsAsync(context, cancellationToken);
        var locations = new List<CsxLocation>();
        foreach (var script in scripts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsCurrentScript(script, context, entryPath))
            {
                continue;
            }

            var candidatePath = TagScriptPath.Normalize(script.Name);
            var candidateScripts = SelectScriptsVisibleToCandidate(scripts, script);
            if (!CandidateCanSeeCurrentDocument(candidateScripts, context, entryPath))
            {
                continue;
            }

            var candidateVisibleScripts = CreateVisibleScriptSetForCandidate(
                candidateScripts,
                context,
                sourceCode,
                documentUri);
            if (!LoadsScript(candidatePath, script.Content, entryPath, candidateVisibleScripts.ContentByPath))
            {
                continue;
            }

            locations.AddRange(MapLocations(
                await language.GetReferencesToTargetAsync(
                    candidatePath,
                    script.Content,
                    referenceTarget.Name,
                    referenceTarget.DeclarationLocations,
                    candidateVisibleScripts.ContentByPath,
                    typeof(TagScriptGlobals),
                    cancellationToken),
                candidatePath,
                BuildDocumentUri(script),
                script.Content,
                candidateVisibleScripts));
        }

        return locations;
    }

    /// <summary>
    /// Загружает скрипты, которые теоретически могут ссылаться на текущий документ.
    /// </summary>
    /// <param name="context">Контекст текущего CSX-документа.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список сохраненных скриптов для reverse-поиска references.</returns>
    private async Task<IReadOnlyList<ScriptFileEntity>> LoadReferenceSearchScriptsAsync(
        CsxDocumentContext context,
        CancellationToken cancellationToken)
    {
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
        var query = db.ScriptFiles.AsNoTracking();
        if (context.Scope != ScriptScope.Shared && !string.IsNullOrWhiteSpace(context.EmulatorId))
        {
            query = query.Where(script => script.Scope == sharedScope || script.EmulatorId == context.EmulatorId);
        }

        return await query
            .OrderBy(script => script.Scope == sharedScope ? 0 : 1)
            .ThenBy(script => script.EmulatorId)
            .ThenBy(script => script.Name)
            .ThenBy(script => script.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Создает набор видимых скриптов так, как его увидит кандидат, который может ссылаться на текущий документ.
    /// </summary>
    /// <param name="candidateScripts">Скрипты, видимые кандидату.</param>
    /// <param name="currentContext">Контекст текущего документа.</param>
    /// <param name="currentSourceCode">Текущий текст документа из редактора.</param>
    /// <param name="currentDocumentUri">URI текущего документа в редакторе.</param>
    /// <returns>Содержимое и URI скриптов, видимых кандидату.</returns>
    private static VisibleScriptSet CreateVisibleScriptSetForCandidate(
        IReadOnlyList<ScriptFileEntity> candidateScripts,
        CsxDocumentContext currentContext,
        string currentSourceCode,
        string? currentDocumentUri)
    {
        var contentByPath = VisibleScriptResolver.ToContentMap(candidateScripts);
        var documentsByPath = CreateVisibleDocumentMap(candidateScripts);

        if (!string.IsNullOrWhiteSpace(currentContext.ScriptName))
        {
            var currentPath = TagScriptPath.Normalize(currentContext.ScriptName);
            VisibleScriptResolver.AddOrReplace(contentByPath, currentPath, currentSourceCode);
            if (!string.IsNullOrWhiteSpace(currentDocumentUri))
            {
                documentsByPath[currentPath] = new VisibleScriptDocument(currentDocumentUri, currentSourceCode);
            }
        }

        return new VisibleScriptSet(contentByPath, documentsByPath);
    }

    /// <summary>
    /// Отбирает скрипты, которые доступны кандидату с учетом shared-области и области его эмулятора.
    /// </summary>
    /// <param name="scripts">Все скрипты, участвующие в поиске.</param>
    /// <param name="candidate">Скрипт-кандидат, из которого ищутся ссылки.</param>
    /// <returns>Список скриптов, видимых кандидату.</returns>
    private static IReadOnlyList<ScriptFileEntity> SelectScriptsVisibleToCandidate(
        IReadOnlyList<ScriptFileEntity> scripts,
        ScriptFileEntity candidate)
    {
        var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
        return scripts
            .Where(script => script.Scope == sharedScope
                             || (!string.IsNullOrWhiteSpace(candidate.EmulatorId)
                                 && string.Equals(script.EmulatorId, candidate.EmulatorId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    /// <summary>
    /// Проверяет, что текущий документ не перекрыт другим скриптом с тем же путем в контексте кандидата.
    /// </summary>
    /// <param name="candidateScripts">Скрипты, видимые кандидату.</param>
    /// <param name="context">Контекст текущего документа.</param>
    /// <param name="entryPath">Нормализованный путь текущего документа.</param>
    /// <returns><see langword="true"/>, если кандидат видит именно текущий документ.</returns>
    private static bool CandidateCanSeeCurrentDocument(
        IReadOnlyList<ScriptFileEntity> candidateScripts,
        CsxDocumentContext context,
        string entryPath)
    {
        var visibleScript = ResolveVisibleScriptByPath(candidateScripts, entryPath);
        return visibleScript is null || IsCurrentScript(visibleScript, context, entryPath);
    }

    /// <summary>
    /// Возвращает скрипт, который победит при разрешении указанного пути в наборе видимых скриптов.
    /// </summary>
    /// <param name="scripts">Скрипты, видимые одному кандидату.</param>
    /// <param name="path">Нормализованный путь скрипта.</param>
    /// <returns>Скрипт-победитель или <see langword="null"/>, если путь отсутствует.</returns>
    private static ScriptFileEntity? ResolveVisibleScriptByPath(
        IReadOnlyList<ScriptFileEntity> scripts,
        string path)
    {
        return scripts
            .Where(script => string.Equals(TagScriptPath.Normalize(script.Name), path, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ScopePriority)
            .ThenBy(script => script.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(script => script.Id, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault();
    }

    /// <summary>
    /// Возвращает приоритет области скрипта для повторения правил <see cref="VisibleScriptResolver"/>.
    /// </summary>
    /// <param name="script">Скрипт из базы данных.</param>
    /// <returns>Числовой приоритет области: shared раньше, emulator позже.</returns>
    private static int ScopePriority(ScriptFileEntity script)
    {
        return string.Equals(script.Scope, UniEmuJson.EnumString(ScriptScope.Shared), StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;
    }

    /// <summary>
    /// Проверяет, является ли сохраненный скрипт текущим документом редактора.
    /// </summary>
    /// <param name="script">Сохраненный скрипт из базы.</param>
    /// <param name="context">Контекст текущего документа.</param>
    /// <param name="entryPath">Нормализованный путь текущего документа.</param>
    /// <returns><see langword="true"/>, если скрипт совпадает с текущим документом.</returns>
    private static bool IsCurrentScript(ScriptFileEntity script, CsxDocumentContext context, string entryPath)
    {
        if (!string.IsNullOrWhiteSpace(context.ScriptId))
        {
            return string.Equals(script.Id, context.ScriptId, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(TagScriptPath.Normalize(script.Name), entryPath, StringComparison.OrdinalIgnoreCase)
               && string.Equals(script.Scope, UniEmuJson.EnumString(context.Scope), StringComparison.OrdinalIgnoreCase)
               && string.Equals(script.EmulatorId, context.EmulatorId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, загружает ли скрипт целевой файл напрямую или через цепочку <c>#load</c>.
    /// </summary>
    /// <param name="candidatePath">Нормализованный путь скрипта-кандидата.</param>
    /// <param name="candidateContent">Содержимое скрипта-кандидата.</param>
    /// <param name="targetPath">Нормализованный путь целевого скрипта.</param>
    /// <param name="scripts">Скрипты, видимые кандидату.</param>
    /// <returns><see langword="true"/>, если кандидат загружает целевой скрипт.</returns>
    private static bool LoadsScript(
        string candidatePath,
        string candidateContent,
        string targetPath,
        IReadOnlyDictionary<string, string> scripts)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return LoadsScript(candidatePath, candidateContent, targetPath, scripts, visited);
    }

    /// <summary>
    /// Рекурсивно обходит граф <c>#load</c> для проверки зависимости от целевого скрипта.
    /// </summary>
    /// <param name="path">Текущий путь в графе загрузок.</param>
    /// <param name="content">Содержимое текущего скрипта.</param>
    /// <param name="targetPath">Нормализованный путь целевого скрипта.</param>
    /// <param name="scripts">Скрипты, видимые исходному кандидату.</param>
    /// <param name="visited">Уже посещенные скрипты для защиты от циклов.</param>
    /// <returns><see langword="true"/>, если найден целевой скрипт.</returns>
    private static bool LoadsScript(
        string path,
        string content,
        string targetPath,
        IReadOnlyDictionary<string, string> scripts,
        HashSet<string> visited)
    {
        if (!visited.Add(path))
        {
            return false;
        }

        foreach (var loadPathValue in s_expander.GetLoadDirectivePaths(content))
        {
            var loadPath = s_expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is null)
            {
                continue;
            }

            if (string.Equals(loadPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (scripts.TryGetValue(loadPath, out var loadedContent)
                && LoadsScript(loadPath, loadedContent, targetPath, scripts, visited))
            {
                return true;
            }
        }

        return false;
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
        string? documentUri,
        string sourceCode,
        VisibleScriptSet visibleScripts)
    {
        return locations
            .Select(location => location with
            {
                DocumentPath = MapDocumentPath(location.DocumentPath, entryPath, documentUri, visibleScripts),
                SourceCode = ResolveLocationSourceCode(location.DocumentPath, entryPath, sourceCode, visibleScripts),
            })
            .ToArray();
    }

    private static IReadOnlyList<CsxDiagnostic> MapDiagnostics(
        IReadOnlyList<CsxDiagnostic> diagnostics,
        string entryPath,
        string? documentUri)
    {
        var targetDocumentPath = string.IsNullOrWhiteSpace(documentUri) ? entryPath : documentUri;
        return diagnostics
            .Select(diagnostic =>
            {
                var documentPath = string.IsNullOrWhiteSpace(diagnostic.DocumentPath)
                    ? entryPath
                    : diagnostic.DocumentPath;
                return diagnostic with
                {
                    DocumentPath = MapDocumentPath(documentPath, entryPath, documentUri),
                };
            })
            .Where(diagnostic => string.Equals(diagnostic.DocumentPath, targetDocumentPath, StringComparison.OrdinalIgnoreCase))
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

    private static string MapDocumentPath(
        string documentPath,
        string entryPath,
        string? documentUri,
        VisibleScriptSet? visibleScripts = null)
    {
        if (string.Equals(documentPath, entryPath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(documentUri))
        {
            return documentUri;
        }

        var normalized = TagScriptPath.Normalize(documentPath);
        return visibleScripts is not null && visibleScripts.DocumentsByPath.TryGetValue(normalized, out var document)
            ? document.DocumentUri
            : documentPath;
    }

    private static string? ResolveLocationSourceCode(
        string documentPath,
        string entryPath,
        string sourceCode,
        VisibleScriptSet visibleScripts)
    {
        if (string.Equals(documentPath, entryPath, StringComparison.OrdinalIgnoreCase))
        {
            return sourceCode;
        }

        var normalized = TagScriptPath.Normalize(documentPath);
        return visibleScripts.DocumentsByPath.TryGetValue(normalized, out var document)
            ? document.SourceCode
            : null;
    }

    private static Dictionary<string, VisibleScriptDocument> CreateVisibleDocumentMap(IEnumerable<ScriptFileEntity> scripts)
    {
        var result = new Dictionary<string, VisibleScriptDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in scripts
                     .OrderBy(script => string.Equals(script.Scope, UniEmuJson.EnumString(ScriptScope.Shared), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(script => script.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(script => script.Id, StringComparer.OrdinalIgnoreCase))
        {
            result[TagScriptPath.Normalize(script.Name)] = new VisibleScriptDocument(
                BuildDocumentUri(script),
                script.Content);
        }

        return result;
    }

    private static string BuildDocumentUri(ScriptFileEntity script)
    {
        var query = $"name={Uri.EscapeDataString(script.Name)}&scope={Uri.EscapeDataString(script.Scope)}";
        if (!string.IsNullOrWhiteSpace(script.EmulatorId))
        {
            query += $"&emulatorId={Uri.EscapeDataString(script.EmulatorId)}";
        }

        return $"uniemu://scripts/{Uri.EscapeDataString(script.Id)}/{Uri.EscapeDataString(script.Name)}?{query}";
    }

    private sealed record VisibleScriptSet(
        IReadOnlyDictionary<string, string> ContentByPath,
        IReadOnlyDictionary<string, VisibleScriptDocument> DocumentsByPath);

    private sealed record VisibleScriptDocument(string DocumentUri, string SourceCode);
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
