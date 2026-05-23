using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Features.Scripts;

/// <summary>
/// HTTP API для управления CSX-скриптами тегов.
/// </summary>
[ApiController]
[Route("api/scripts")]
public sealed class ScriptsController(ScriptService service) : ControllerBase
{
    /// <summary>
    /// Возвращает скрипты с опциональной фильтрацией по области видимости и эмулятору.
    /// </summary>
    /// <param name="scope">Область видимости скрипта.</param>
    /// <param name="emulatorId">Идентификатор эмулятора для эмуляторных скриптов.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список скриптов.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScriptFileDto>>> List([FromQuery] ScriptScope? scope, [FromQuery] string? emulatorId, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(scope, emulatorId, cancellationToken));
    }

    /// <summary>
    /// Создает новый CSX-скрипт.
    /// </summary>
    /// <param name="request">Параметры создаваемого скрипта.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданный скрипт или ответ 400 при некорректной области видимости.</returns>
    [HttpPost]
    public async Task<ActionResult<ScriptFileDto>> Create(CreateScriptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        try
        {
            var script = await service.CreateAsync(request, cancellationToken);
            return script is null ? BadRequest("Invalid scope/emulatorId combination.") : CreatedAtAction(nameof(List), script);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Обновляет имя и/или содержимое CSX-скрипта.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <param name="request">Новые значения скрипта.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Обновленный скрипт, 404 если он не найден или 400 при ошибке валидации кода.</returns>
    [HttpPatch("{scriptId}")]
    public async Task<ActionResult<ScriptFileDto>> Patch(string scriptId, PatchScriptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var script = await service.PatchAsync(scriptId, request, cancellationToken);
            return script is null ? NotFound() : Ok(script);
        }
        catch (CsxScriptValidationException ex)
        {
            return BadRequest(new
            {
                message = "CSX script validation failed.",
                diagnostics = ex.Diagnostics,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Удаляет CSX-скрипт.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Ответ 204 при удалении или 404, если скрипт не найден.</returns>
    [HttpDelete("{scriptId}")]
    public async Task<IActionResult> Delete(string scriptId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(scriptId, cancellationToken) ? NoContent() : NotFound();
    }
}
