using Microsoft.AspNetCore.Mvc;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.CncPrograms;

[ApiController]
public sealed class CncProgramsController(CncProgramService service) : ControllerBase
{
    [HttpGet("api/cnc-programs")]
    public async Task<ActionResult<IReadOnlyList<CncProgramDto>>> List([FromQuery] CncScope? scope, [FromQuery] string? emulatorId, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(scope, emulatorId, cancellationToken));
    }

    [HttpPost("api/cnc-programs")]
    public async Task<ActionResult<CncProgramDto>> Create(CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var program = await service.CreateAsync(request, cancellationToken);
        return program is null ? BadRequest("Invalid scope/emulatorId combination.") : Created("api/cnc-programs", program);
    }

    [HttpPost("api/emulators/{emulatorId}/cnc-programs")]
    public async Task<ActionResult<CncProgramDto>> CreateForEmulator(string emulatorId, CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        var program = await service.CreateForEmulatorAsync(emulatorId, request, cancellationToken);
        return program is null ? NotFound() : Created("api/cnc-programs", program);
    }

    [HttpPatch("api/cnc-programs/{programId}")]
    public async Task<ActionResult<CncProgramDto>> Patch(string programId, PatchCncProgramRequest request, CancellationToken cancellationToken)
    {
        var program = await service.PatchAsync(programId, request, cancellationToken);
        return program is null ? NotFound() : Ok(program);
    }

    [HttpDelete("api/cnc-programs/{programId}")]
    public async Task<IActionResult> Delete(string programId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(programId, cancellationToken) ? NoContent() : NotFound();
    }
}
