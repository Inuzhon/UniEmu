using Microsoft.Extensions.Options;
using UniEmu.Features.Settings;
using UniEmu.Hosting;

namespace UniEmu.Tests.Features.Settings;

public sealed class SettingsControllerTests
{
    [Fact]
    public void Get_ReturnsConfiguredDefaultTargetUrl()
    {
        var controller = new SettingsController(Options.Create(new UniEmuOptions
        {
            DefaultTargetUrl = "http://host.docker.internal:8080",
        }));

        var settings = controller.Get();

        Assert.Equal("http://host.docker.internal:8080", settings.DefaultTargetUrl);
    }
}
