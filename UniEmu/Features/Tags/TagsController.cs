using Microsoft.AspNetCore.Mvc;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Tags;

[ApiController]
[Route("api/emulators/{emulatorId}/tags")]
public sealed class TagsController(TagService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmulatorTagDto>>> List(string emulatorId, CancellationToken cancellationToken)
    {
        var tags = await service.ListAsync(emulatorId, cancellationToken);
        return tags is null ? NotFound() : Ok(tags);
    }

    [HttpPost]
    public async Task<ActionResult<EmulatorTagDto>> Create(string emulatorId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest("Name and key are required.");
        }

        var tag = await service.CreateAsync(emulatorId, request, cancellationToken);
        return tag is null ? NotFound() : CreatedAtAction(nameof(List), new { emulatorId }, tag);
    }

    [HttpPatch("{tagId}")]
    public async Task<ActionResult<EmulatorTagDto>> Replace(string emulatorId, string tagId, ReplaceTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await service.ReplaceAsync(emulatorId, tagId, request, cancellationToken);
        return tag is null ? NotFound() : Ok(tag);
    }

    [HttpDelete("{tagId}")]
    public async Task<IActionResult> Delete(string emulatorId, string tagId, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(emulatorId, tagId, cancellationToken) ? NoContent() : NotFound();
    }
}
