using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Telemetry;

/// <summary>
/// HTTP API для чтения и ручной записи телеметрии эмуляторов.
/// </summary>
[ApiController]
public sealed class TelemetryController(TelemetryService service) : ControllerBase
{
    /// <summary>
    /// Возвращает последние точки телеметрии эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="points">Запрошенное количество точек.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список точек телеметрии или ответ 404, если эмулятор не найден.</returns>
    [HttpGet("api/emulators/{emulatorId}/telemetry")]
    public async Task<ActionResult<IReadOnlyList<TelemetryPointDto>>> Get(string emulatorId, [FromQuery] int points, CancellationToken cancellationToken)
    {
        var telemetry = await service.GetAsync(emulatorId, points, cancellationToken);
        return telemetry is null ? NotFound() : Ok(telemetry);
    }

    /// <summary>
    /// Записывает точку телеметрии для эмулятора.
    /// </summary>
    /// <param name="request">Данные телеметрии.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданная точка телеметрии или ответ 404, если эмулятор не найден.</returns>
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
