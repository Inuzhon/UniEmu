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
                ["UniEmu:EnableStaticAssetCompression"] = "true",
                ["UniEmu:EnableStaticAssetCaching"] = "true",
                ["UniEmu:DefaultTargetUrl"] = "http://dispatcher.local:9080",
                ["UniEmu:DispatcherBlockCheckIntervalSeconds"] = "9",
                ["UniEmu:ScriptExecutionTimeoutSeconds"] = "7",
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
        Assert.True(options.EnableStaticAssetCompression);
        Assert.True(options.EnableStaticAssetCaching);
        Assert.Equal("http://dispatcher.local:9080", options.DefaultTargetUrl);
        Assert.Equal(9, options.DispatcherBlockCheckIntervalSeconds);
        Assert.Equal(7, options.ScriptExecutionTimeoutSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void AddUniEmuOptions_ClampsDispatcherBlockCheckIntervalToAtLeastOne(string interval)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UniEmu:DispatcherBlockCheckIntervalSeconds"] = interval,
            })
            .Build();
        var services = new ServiceCollection();

        services.AddUniEmuOptions(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<UniEmuOptions>>().Value;
        Assert.Equal(1, options.DispatcherBlockCheckIntervalSeconds);
    }
}
