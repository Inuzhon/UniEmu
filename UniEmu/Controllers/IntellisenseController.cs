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
