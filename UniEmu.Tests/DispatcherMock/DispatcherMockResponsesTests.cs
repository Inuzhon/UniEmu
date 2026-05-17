using UniEmu.DispatcherMock;

namespace UniEmu.Tests.DispatcherMock;

public sealed class DispatcherMockResponsesTests
{
    [Fact]
    public void MonitoringAnswer_ReturnsSuccessfulNoFileExchangeMarkers()
    {
        Assert.Equal("FileType=0;GetFile=0", DispatcherMockResponses.MonitoringAccepted);
    }

    [Fact]
    public void ProgramUploadAnswer_ReturnsOk()
    {
        Assert.Equal("ok", DispatcherMockResponses.ProgramUploadAccepted);
    }

    [Fact]
    public void MonitoringBlockedAnswer_ReturnsNotBlockedFlag()
    {
        Assert.Equal("0", DispatcherMockResponses.MonitoringNotBlocked);
    }
}
