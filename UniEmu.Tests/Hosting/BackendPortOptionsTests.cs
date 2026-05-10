using Microsoft.Extensions.Configuration;
using UniEmu.Hosting;

namespace UniEmu.Tests.Hosting;

public sealed class BackendPortOptionsTests
{
    [Fact]
    public void Resolve_UsesDefaultPort_WhenConfigurationIsMissing()
    {
        var configuration = BuildConfiguration();

        var options = BackendPortOptions.Resolve(configuration);

        Assert.Equal(5083, options.Port);
        Assert.Equal("http://0.0.0.0:5083", options.HttpUrl);
    }

    [Fact]
    public void Resolve_UsesConfiguredPort_WhenConfigurationContainsPort()
    {
        var configuration = BuildConfiguration(("UniEmu:Port", "6010"));

        var options = BackendPortOptions.Resolve(configuration);

        Assert.Equal(6010, options.Port);
        Assert.Equal("http://0.0.0.0:6010", options.HttpUrl);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        var configurationValues = values.ToDictionary(
            pair => pair.Key,
            pair => (string?)pair.Value,
            StringComparer.OrdinalIgnoreCase);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
    }
}
