using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Events;

/// <summary>
/// HTTP API для чтения и публикации системных событий.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController(EventService service) : ControllerBase
{
    /// <summary>
    /// Возвращает ленту системных событий.
    /// </summary>
    /// <param name="cursor">Временная метка, старше которой нужно вернуть события.</param>
    /// <param name="limit">Максимальное количество событий.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список событий, отсортированный от новых к старым.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemEventDto>>> List([FromQuery] DateTimeOffset? cursor, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(cursor, limit, cancellationToken));
    }

    /// <summary>
    /// Публикует новое системное событие.
    /// </summary>
    /// <param name="request">Данные создаваемого события.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданное событие.</returns>
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
