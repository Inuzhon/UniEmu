using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.CncPrograms;

/// <summary>
/// HTTP API для управления CNC-программами.
/// </summary>
[ApiController]
public sealed class CncProgramsController(CncProgramService service) : ControllerBase
{
    /// <summary>
    /// Возвращает CNC-программы с опциональной фильтрацией по области видимости и эмулятору.
    /// </summary>
    /// <param name="scope">Область видимости программы.</param>
    /// <param name="emulatorId">Идентификатор эмулятора для эмуляторных программ.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список CNC-программ.</returns>
    [HttpGet("api/cnc-programs")]
    public async Task<ActionResult<IReadOnlyList<CncProgramDto>>> List([FromQuery] CncScope? scope, [FromQuery] string? emulatorId, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(scope, emulatorId, cancellationToken));
    }

    /// <summary>
    /// Создает общую или привязанную к эмулятору CNC-программу.
    /// </summary>
    /// <param name="request">Параметры создаваемой программы.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданная программа или ответ 400 при некорректной области видимости.</returns>
    [HttpPost("api/cnc-programs")]
    public async Task<ActionResult<CncProgramDto>> Create(CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        try
        {
            var program = await service.CreateAsync(request, cancellationToken);
            return program is null ? BadRequest("Invalid scope/emulatorId combination.") : Created("api/cnc-programs", program);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Создает CNC-программу для конкретного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Параметры создаваемой программы.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданная программа или ответ 404, если эмулятор не найден.</returns>
    [HttpPost("api/emulators/{emulatorId}/cnc-programs")]
    public async Task<ActionResult<CncProgramDto>> CreateForEmulator(string emulatorId, CreateCncProgramRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var program = await service.CreateForEmulatorAsync(emulatorId, request, cancellationToken);
            return program is null ? NotFound() : Created("api/cnc-programs", program);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Обновляет метаданные и/или содержимое CNC-программы.
    /// </summary>
    /// <param name="programId">Идентификатор программы.</param>
    /// <param name="request">Новые значения программы.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Обновленная программа или ответ 404, если она не найдена.</returns>
    [HttpPatch("api/cnc-programs/{programId}")]
    public async Task<ActionResult<CncProgramDto>> Patch(string programId, PatchCncProgramRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var program = await service.PatchAsync(programId, request, cancellationToken);
            return program is null ? NotFound() : Ok(program);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Удаляет CNC-программу.
    /// </summary>
    /// <param name="programId">Идентификатор программы.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Ответ 204 при удалении или 404, если программа не найдена.</returns>
    [HttpDelete("api/cnc-programs/{programId}")]
    public async Task<IActionResult> Delete(string programId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(programId, cancellationToken) ? NoContent() : NotFound();
    }
}
