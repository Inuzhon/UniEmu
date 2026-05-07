using Microsoft.AspNetCore.Mvc;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.Emulators;

[ApiController]
[Route("api/emulators")]
public sealed class EmulatorsController(EmulatorService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmulatorDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(cancellationToken));
    }

    [HttpGet("{emulatorId}")]
    public async Task<ActionResult<EmulatorDto>> Get(string emulatorId, CancellationToken cancellationToken)
    {
        var emulator = await service.GetAsync(emulatorId, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }

    [HttpPost]
    public async Task<ActionResult<EmulatorDto>> Create(CreateEmulatorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            return BadRequest("Name and targetUrl are required.");
        }

        var emulator = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { emulatorId = emulator.Id }, emulator);
    }

    [HttpPatch("{emulatorId}")]
    public async Task<ActionResult<EmulatorDto>> Patch(string emulatorId, PatchEmulatorRequest request, CancellationToken cancellationToken)
    {
        var emulator = await service.PatchAsync(emulatorId, request, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }

    [HttpPatch("{emulatorId}/status")]
    public async Task<ActionResult<EmulatorDto>> PatchStatus(string emulatorId, PatchEmulatorStatusRequest request, CancellationToken cancellationToken)
    {
        var emulator = await service.PatchStatusAsync(emulatorId, request, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }
}
