using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Features.Tags;

/// <summary>
/// HTTP API для управления тегами конкретного эмулятора.
/// </summary>
[ApiController]
[Route("api/emulators/{emulatorId}/tags")]
public sealed class TagsController(TagService service) : ControllerBase
{
    /// <summary>
    /// Возвращает теги эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Список тегов или ответ 404, если эмулятор не найден.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmulatorTagDto>>> List(string emulatorId, CancellationToken cancellationToken)
    {
        var tags = await service.ListAsync(emulatorId, cancellationToken);
        return tags is null ? NotFound() : Ok(tags);
    }

    /// <summary>
    /// Создает тег внутри эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Параметры создаваемого тега.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Созданный тег, 404 для неизвестного эмулятора или 400 при ошибке валидации скрипта.</returns>
    [HttpPost]
    public async Task<ActionResult<EmulatorTagDto>> Create(string emulatorId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest("Name and key are required.");
        }

        try
        {
            var tag = await service.CreateAsync(emulatorId, request, cancellationToken);
            return tag is null ? NotFound() : CreatedAtAction(nameof(List), new { emulatorId }, tag);
        }
        catch (CsxScriptValidationException ex)
        {
            return BadRequest(new
            {
                message = "CSX script validation failed.",
                diagnostics = ex.Diagnostics,
            });
        }
    }

    /// <summary>
    /// Полностью заменяет конфигурацию тега.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="request">Новая конфигурация тега.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Обновленный тег, 404 если тег не найден или 400 при ошибке валидации скрипта.</returns>
    [HttpPatch("{tagId}")]
    public async Task<ActionResult<EmulatorTagDto>> Replace(string emulatorId, string tagId, ReplaceTagRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tag = await service.ReplaceAsync(emulatorId, tagId, request, cancellationToken);
            return tag is null ? NotFound() : Ok(tag);
        }
        catch (CsxScriptValidationException ex)
        {
            return BadRequest(new
            {
                message = "CSX script validation failed.",
                diagnostics = ex.Diagnostics,
            });
        }
    }

    /// <summary>
    /// Удаляет тег эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <returns>Ответ 204 при удалении или 404, если тег не найден.</returns>
    [HttpDelete("{tagId}")]
    public async Task<IActionResult> Delete(string emulatorId, string tagId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(emulatorId, tagId, cancellationToken) ? NoContent() : NotFound();
    }
}
