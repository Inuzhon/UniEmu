using Microsoft.AspNetCore.Mvc;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.Events;

[ApiController]
[Route("api/events")]
public sealed class EventsController(EventService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemEventDto>>> List([FromQuery] DateTimeOffset? cursor, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(cursor, limit, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<SystemEventDto>> Create(PushEventRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmulatorId) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("emulatorId and message are required.");
        }

        var ev = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), ev);
    }
}
