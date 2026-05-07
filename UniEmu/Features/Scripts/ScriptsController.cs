using Microsoft.AspNetCore.Mvc;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.Scripts;

[ApiController]
[Route("api/scripts")]
public sealed class ScriptsController(ScriptService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScriptFileDto>>> List([FromQuery] ScriptScope? scope, [FromQuery] string? emulatorId, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(scope, emulatorId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ScriptFileDto>> Create(CreateScriptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var script = await service.CreateAsync(request, cancellationToken);
        return script is null ? BadRequest("Invalid scope/emulatorId combination.") : CreatedAtAction(nameof(List), script);
    }

    [HttpPatch("{scriptId}")]
    public async Task<ActionResult<ScriptFileDto>> Patch(string scriptId, PatchScriptRequest request, CancellationToken cancellationToken)
    {
        var script = await service.PatchAsync(scriptId, request, cancellationToken);
        return script is null ? NotFound() : Ok(script);
    }

    [HttpDelete("{scriptId}")]
    public async Task<IActionResult> Delete(string scriptId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(scriptId, cancellationToken) ? NoContent() : NotFound();
    }
}
