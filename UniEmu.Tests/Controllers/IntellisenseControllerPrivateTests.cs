using System.Reflection;
using UniEmu.Controllers;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Controllers;

public sealed class IntellisenseControllerPrivateTests
{
    [Fact]
    public void IsSourceTooLarge_ReturnsTrueOnlyWhenSourceExceedsLimit()
    {
        Assert.False(InvokeIsSourceTooLarge(new CsxIntellisenseRequest(null, null, null)));
        Assert.False(InvokeIsSourceTooLarge(new CsxIntellisenseRequest(new string('x', 20_000), null, null)));
        Assert.True(InvokeIsSourceTooLarge(new CsxIntellisenseRequest(new string('x', 20_001), null, null)));
    }

    [Fact]
    public void ClampPosition_ClampsLineAndColumn_AndPreservesOtherRequestFields()
    {
        var request = new CsxIntellisenseRequest(
            "return 1;",
            "script://inline/test.csx",
            new CsxEditorPosition(-5, 25_000),
            new CsxTextRange(1, 1, 1, 8),
            "Renamed",
            IncludeDeclaration: false);

        var clamped = InvokeClampPosition(request);

        Assert.NotSame(request, clamped);
        Assert.Equal(new CsxEditorPosition(1, 10_000), clamped.Position);
        Assert.Equal(request.SourceCode, clamped.SourceCode);
        Assert.Equal(request.DocumentUri, clamped.DocumentUri);
        Assert.Equal(request.Range, clamped.Range);
        Assert.Equal(request.NewName, clamped.NewName);
        Assert.False(clamped.IncludeDeclaration);
    }

    [Fact]
    public void ClampPosition_ReturnsOriginalRequest_WhenPositionIsNull()
    {
        var request = new CsxIntellisenseRequest("return 1;", null, null);

        var clamped = InvokeClampPosition(request);

        Assert.Same(request, clamped);
    }

    private static bool InvokeIsSourceTooLarge(CsxIntellisenseRequest request)
    {
        var method = typeof(IntellisenseController).GetMethod(
            "IsSourceTooLarge",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [request]));
    }

    private static CsxIntellisenseRequest InvokeClampPosition(CsxIntellisenseRequest request)
    {
        var method = typeof(IntellisenseController).GetMethod(
            "ClampPosition",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<CsxIntellisenseRequest>(method.Invoke(null, [request]));
    }
}
