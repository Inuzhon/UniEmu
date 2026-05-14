using Microsoft.AspNetCore.Mvc;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Controllers;

/// <summary>
/// HTTP API для языковых возможностей CSX-редактора.
/// </summary>
[ApiController]
[Route("api/intellisense/csharp")]
public sealed class IntellisenseController(CsxIntellisenseService service) : ControllerBase
{
    private const int MaxSourceCodeLength = 20_000;

    /// <summary>
    /// Анализирует CSX-код и возвращает диагностические сообщения.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и контекстом документа.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список диагностик компиляции и валидации.</returns>
    [HttpPost("diagnostics")]
    public async Task<ActionResult<IReadOnlyList<CsxDiagnostic>>> Diagnostics(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request))
        {
            return BadRequest("Source code is too large.");
        }

        return Ok(await service.GetDiagnosticsAsync(request, cancellationToken));
    }

    /// <summary>
    /// Возвращает варианты автодополнения для позиции в CSX-коде.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом, позицией курсора и контекстом документа.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список элементов автодополнения.</returns>
    [HttpPost("completions")]
    public async Task<ActionResult<IReadOnlyList<CsxCompletionItem>>> Completions(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request))
        {
            return BadRequest("Source code is too large.");
        }

        return Ok(await service.GetCompletionsAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает hover-информацию для символа под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Описание символа или <see langword="null"/>, если данных нет.</returns>
    [HttpPost("hover")]
    public async Task<ActionResult<CsxHover?>> Hover(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request))
        {
            return BadRequest("Source code is too large.");
        }

        return Ok(await service.GetHoverAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает подсказку сигнатуры вызываемого метода.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Сигнатуры перегрузок или <see langword="null"/>, если курсор не находится в вызове.</returns>
    [HttpPost("signature-help")]
    public async Task<ActionResult<CsxSignatureHelp?>> SignatureHelp(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request))
        {
            return BadRequest("Source code is too large.");
        }

        return Ok(await service.GetSignatureHelpAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает определения символа под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список мест определения.</returns>
    [HttpPost("definition")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> Definition(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetDefinitionsAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает определения типа символа под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список мест определения типа.</returns>
    [HttpPost("type-definition")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> TypeDefinition(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetTypeDefinitionsAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает ссылки на символ под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список мест использования символа.</returns>
    [HttpPost("references")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> References(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetReferencesAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает реализации символа под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список мест реализации.</returns>
    [HttpPost("implementation")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> Implementation(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetImplementationsAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Готовит правки для переименования символа.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом, позицией и новым именем.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Набор правок рабочего пространства или <see langword="null"/>, если переименование недоступно.</returns>
    [HttpPost("rename")]
    public async Task<ActionResult<CsxWorkspaceEdit?>> Rename(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.RenameAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Форматирует весь CSX-документ.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом документа.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    [HttpPost("format")]
    public async Task<ActionResult<IReadOnlyList<CsxTextEdit>>> Format(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.FormatDocumentAsync(request, cancellationToken));
    }

    /// <summary>
    /// Форматирует выбранный диапазон CSX-документа.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и диапазоном форматирования.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список текстовых правок форматирования.</returns>
    [HttpPost("format-range")]
    public async Task<ActionResult<IReadOnlyList<CsxTextEdit>>> FormatRange(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.FormatRangeAsync(request, cancellationToken));
    }

    /// <summary>
    /// Возвращает сворачиваемые диапазоны CSX-документа.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом документа.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список диапазонов для folding.</returns>
    [HttpPost("folding-ranges")]
    public async Task<ActionResult<IReadOnlyList<CsxFoldingRange>>> FoldingRanges(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetFoldingRangesAsync(request, cancellationToken));
    }

    /// <summary>
    /// Возвращает semantic tokens для подсветки CSX-документа.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом документа.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Легенда и поток semantic tokens.</returns>
    [HttpPost("semantic-tokens")]
    public async Task<ActionResult<CsxSemanticTokens>> SemanticTokens(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetSemanticTokensAsync(request, cancellationToken));
    }

    /// <summary>
    /// Подготавливает элемент call hierarchy для символа под курсором.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией курсора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список элементов call hierarchy.</returns>
    [HttpPost("call-hierarchy/prepare")]
    public async Task<ActionResult<IReadOnlyList<CsxCallHierarchyItem>>> PrepareCallHierarchy(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.PrepareCallHierarchyAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает входящие вызовы для элемента call hierarchy.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией элемента.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список входящих вызовов.</returns>
    [HttpPost("call-hierarchy/incoming")]
    public async Task<ActionResult<IReadOnlyList<CsxCallHierarchyIncomingCall>>> IncomingCalls(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetIncomingCallsAsync(ClampPosition(request), cancellationToken));
    }

    /// <summary>
    /// Возвращает исходящие вызовы для элемента call hierarchy.
    /// </summary>
    /// <param name="request">Запрос с исходным кодом и позицией элемента.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список исходящих вызовов.</returns>
    [HttpPost("call-hierarchy/outgoing")]
    public async Task<ActionResult<IReadOnlyList<CsxCallHierarchyOutgoingCall>>> OutgoingCalls(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetOutgoingCallsAsync(ClampPosition(request), cancellationToken));
    }

    private static bool IsSourceTooLarge(CsxIntellisenseRequest request)
    {
        return request.SourceCode?.Length > MaxSourceCodeLength;
    }

    private static CsxIntellisenseRequest ClampPosition(CsxIntellisenseRequest request)
    {
        if (request.Position is null)
        {
            return request;
        }

        return request with
        {
            Position = new CsxEditorPosition(
                Math.Clamp(request.Position.Line, 1, 10_000),
                Math.Clamp(request.Position.Column, 1, 10_000)),
        };
    }
}
