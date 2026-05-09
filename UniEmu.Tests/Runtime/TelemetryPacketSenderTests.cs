using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TelemetryPacketSenderTests
{
    [Fact]
    public void ParseMonitoringAnswer_ReadsDispatcherFlags()
    {
        var answer = TelemetryPacketSender.ParseMonitoringAnswer("FileType=2;GetFile=1");

        Assert.Equal(2, answer.FileType);
        Assert.Equal(1, answer.GetFile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("error")]
    [InlineData("FileType=abc;GetFile=1")]
    public void ParseMonitoringAnswer_ThrowsForDispatcherErrors(string content)
    {
        Assert.Throws<DispatcherProtocolException>(() => TelemetryPacketSender.ParseMonitoringAnswer(content));
    }

    [Fact]
    public async Task SendProgramAsync_ThrowsWhenDispatcherDoesNotReturnOk()
    {
        var sender = CreateSender(new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty),
        }));

        var program = new DispatcherProgram("main.nc", Encoding.UTF8.GetBytes("M30"));

        await Assert.ThrowsAsync<DispatcherProtocolException>(() =>
            sender.SendProgramAsync("http://dispatcher", 18, useInnerId: true, program, CancellationToken.None));
    }

    [Fact]
    public async Task GetIsMonitoringBlockedAsync_UsesProtocolIdAsMachineQuery()
    {
        Uri? requestedUri = null;
        var sender = CreateSender(new QueueHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("1"),
            };
        }));

        var blocked = await sender.GetIsMonitoringBlockedAsync("http://dispatcher/api", 18, CancellationToken.None);

        Assert.True(blocked);
        Assert.Equal("/IndustryManagment/WebIntegration/GetIsMonitoringBlocked", requestedUri?.AbsolutePath);
        Assert.Equal("?machine_id=18", requestedUri?.Query);
    }

    private static TelemetryPacketSender CreateSender(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TelemetryPacketSender)))
            .Returns(new HttpClient(handler));

        return new TelemetryPacketSender(factory.Object, NullLogger<TelemetryPacketSender>.Instance);
    }

    private sealed class QueueHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handle(request));
        }
    }
}
