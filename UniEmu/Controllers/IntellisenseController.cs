using Microsoft.AspNetCore.Mvc;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Controllers;

[ApiController]
[Route("api/intellisense/csharp")]
public sealed class IntellisenseController(CsxIntellisenseService service) : ControllerBase
{
    private const int MaxSourceCodeLength = 20_000;

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

    [HttpPost("definition")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> Definition(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetDefinitionsAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("type-definition")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> TypeDefinition(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetTypeDefinitionsAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("references")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> References(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetReferencesAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("implementation")]
    public async Task<ActionResult<IReadOnlyList<CsxLocation>>> Implementation(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetImplementationsAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("rename")]
    public async Task<ActionResult<CsxWorkspaceEdit?>> Rename(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.RenameAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("format")]
    public async Task<ActionResult<IReadOnlyList<CsxTextEdit>>> Format(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.FormatDocumentAsync(request, cancellationToken));
    }

    [HttpPost("format-range")]
    public async Task<ActionResult<IReadOnlyList<CsxTextEdit>>> FormatRange(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.FormatRangeAsync(request, cancellationToken));
    }

    [HttpPost("folding-ranges")]
    public async Task<ActionResult<IReadOnlyList<CsxFoldingRange>>> FoldingRanges(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetFoldingRangesAsync(request, cancellationToken));
    }

    [HttpPost("semantic-tokens")]
    public async Task<ActionResult<CsxSemanticTokens>> SemanticTokens(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetSemanticTokensAsync(request, cancellationToken));
    }

    [HttpPost("call-hierarchy/prepare")]
    public async Task<ActionResult<IReadOnlyList<CsxCallHierarchyItem>>> PrepareCallHierarchy(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.PrepareCallHierarchyAsync(ClampPosition(request), cancellationToken));
    }

    [HttpPost("call-hierarchy/incoming")]
    public async Task<ActionResult<IReadOnlyList<CsxCallHierarchyIncomingCall>>> IncomingCalls(
        CsxIntellisenseRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSourceTooLarge(request)) return BadRequest("Source code is too large.");
        return Ok(await service.GetIncomingCallsAsync(ClampPosition(request), cancellationToken));
    }

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
