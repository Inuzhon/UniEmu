using Microsoft.AspNetCore.Mvc;
using UniEmu.Features.Contracts;

namespace UniEmu.Features.Telemetry;

[ApiController]
public sealed class TelemetryController(TelemetryService service) : ControllerBase
{
    [HttpGet("api/emulators/{emulatorId}/telemetry")]
    public async Task<ActionResult<IReadOnlyList<TelemetryPointDto>>> Get(string emulatorId, [FromQuery] int points, CancellationToken cancellationToken)
    {
        var telemetry = await service.GetAsync(emulatorId, points, cancellationToken);
        return telemetry is null ? NotFound() : Ok(telemetry);
    }

    [HttpPost("api/telemetry/ingest")]
    public async Task<ActionResult<TelemetryPointDto>> Ingest(TelemetryIngestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmulatorId) || request.Values.Count == 0)
        {
            return BadRequest("emulatorId and values are required.");
        }

        var telemetry = await service.IngestAsync(request, cancellationToken);
        return telemetry is null ? NotFound() : Ok(telemetry);
    }
}
