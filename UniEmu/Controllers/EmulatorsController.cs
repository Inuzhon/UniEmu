using System.Text;
using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Emulators;

/// <summary>
/// HTTP API для управления эмуляторами и их состоянием выполнения.
/// </summary>
[ApiController]
[Route("api/emulators")]
public sealed class EmulatorsController(
    EmulatorService service,
    DispatcherTemplateService dispatcherTemplateService) : ControllerBase
{
    /// <summary>
    /// Возвращает список всех эмуляторов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список эмуляторов с краткой статистикой.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmulatorDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(cancellationToken));
    }

    /// <summary>
    /// Возвращает эмулятор по идентификатору.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Эмулятор или ответ 404, если он не найден.</returns>
    [HttpGet("{emulatorId}")]
    public async Task<ActionResult<EmulatorDto>> Get(string emulatorId, CancellationToken cancellationToken)
    {
        var emulator = await service.GetAsync(emulatorId, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }

    /// <summary>
    /// Returns a Universal dispatcher protocol XML template for an emulator.
    /// </summary>
    /// <param name="emulatorId">Emulator identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>XML template file or 404 when emulator is not found.</returns>
    [HttpGet("{emulatorId}/dispatcher-template")]
    public async Task<IActionResult> GetDispatcherTemplate(string emulatorId, CancellationToken cancellationToken)
    {
        var template = await dispatcherTemplateService.CreateAsync(emulatorId, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return File(
            Encoding.UTF8.GetBytes(template.Content),
            "application/xml; charset=utf-8",
            template.FileName);
    }

    /// <summary>
    /// Создает новый эмулятор.
    /// </summary>
    /// <param name="request">Параметры создаваемого эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданный эмулятор.</returns>
    [HttpPost]
    public async Task<ActionResult<EmulatorDto>> Create(CreateEmulatorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            return BadRequest("Name and targetUrl are required.");
        }

        if (request.ProtocolId <= 0)
        {
            return BadRequest("protocolId must be a positive number.");
        }

        var emulator = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { emulatorId = emulator.Id }, emulator);
    }

    /// <summary>
    /// Частично обновляет настройки эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Новые значения изменяемых полей.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Обновленный эмулятор или ответ 404, если он не найден.</returns>
    [HttpPatch("{emulatorId}")]
    public async Task<ActionResult<EmulatorDto>> Patch(string emulatorId, PatchEmulatorRequest request, CancellationToken cancellationToken)
    {
        if (request.ProtocolId is <= 0)
        {
            return BadRequest("protocolId must be a positive number.");
        }

        var emulator = await service.PatchAsync(emulatorId, request, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }

    /// <summary>
    /// Изменяет рабочий статус эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Новый статус эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Обновленный эмулятор или ответ 404, если он не найден.</returns>
    [HttpPatch("{emulatorId}/status")]
    public async Task<ActionResult<EmulatorDto>> PatchStatus(string emulatorId, PatchEmulatorStatusRequest request, CancellationToken cancellationToken)
    {
        var emulator = await service.PatchStatusAsync(emulatorId, request, cancellationToken);
        return emulator is null ? NotFound() : Ok(emulator);
    }

    /// <summary>
    /// Удаляет эмулятор.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Ответ 204 при удалении или 404, если эмулятор не найден.</returns>
    [HttpDelete("{emulatorId}")]
    public async Task<IActionResult> Delete(string emulatorId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(emulatorId, cancellationToken) ? NoContent() : NotFound();
    }
}
