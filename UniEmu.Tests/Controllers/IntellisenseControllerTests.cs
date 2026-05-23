using Microsoft.AspNetCore.Mvc;
using UniEmu.Controllers;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Controllers;

public sealed class IntellisenseControllerTests
{
    [Fact]
    public async Task Endpoints_ReturnBadRequest_WhenSourceCodeIsTooLarge()
    {
        var controller = new IntellisenseController(null!);
        var request = new CsxIntellisenseRequest(new string('x', 20_001), null, new CsxEditorPosition(1, 1));

        AssertSourceTooLarge(await controller.Diagnostics(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Completions(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Hover(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.SignatureHelp(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Definition(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.TypeDefinition(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.References(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Implementation(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Rename(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.Format(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.FormatRange(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.FoldingRanges(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.SemanticTokens(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.PrepareCallHierarchy(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.IncomingCalls(request, CancellationToken.None));
        AssertSourceTooLarge(await controller.OutgoingCalls(request, CancellationToken.None));
    }

    private static void AssertSourceTooLarge<T>(ActionResult<T> result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Source code is too large.", badRequest.Value);
    }
}
