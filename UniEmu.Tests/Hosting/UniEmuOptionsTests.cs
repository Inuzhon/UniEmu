using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniEmu.Hosting;

namespace UniEmu.Tests.Hosting;

public sealed class UniEmuOptionsTests
{
    [Fact]
    public void AddUniEmuOptions_BindsUniEmuSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UniEmu:DisableRuntime"] = "true",
                ["UniEmu:SkipStartupDatabase"] = "true",
                ["UniEmu:SeedData"] = "true",
                ["UniEmu:DisableStaticAssets"] = "true",
                ["UniEmu:DispatcherBlockCheckIntervalSeconds"] = "9",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddUniEmuOptions(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<UniEmuOptions>>().Value;
        Assert.True(options.DisableRuntime);
        Assert.True(options.SkipStartupDatabase);
        Assert.True(options.SeedData);
        Assert.True(options.DisableStaticAssets);
        Assert.Equal(9, options.DispatcherBlockCheckIntervalSeconds);
    }
}
