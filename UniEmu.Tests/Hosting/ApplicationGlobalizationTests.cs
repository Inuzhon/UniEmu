using System.Globalization;
using Microsoft.Extensions.Configuration;
using UniEmu.Hosting;

namespace UniEmu.Tests.Hosting;

[Collection(ApplicationGlobalizationCollection.Name)]
public sealed class ApplicationGlobalizationTests : IDisposable
{
    private readonly CultureInfo? originalCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? originalUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly CultureInfo originalThreadCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo originalThreadUiCulture = CultureInfo.CurrentUICulture;
    private readonly string? originalTimeZone = Environment.GetEnvironmentVariable("TZ");

    [Fact]
    public void Resolve_UsesMoscowAndRussianDefaults_WhenConfigurationIsMissing()
    {
        var configuration = BuildConfiguration();

        var options = ApplicationGlobalizationOptions.Resolve(configuration);

        Assert.Equal("Europe/Moscow", options.TimeZone);
        Assert.Equal("ru-RU", options.Culture);
    }

    [Fact]
    public void Resolve_UsesConfiguredTimeZoneAndCulture()
    {
        var configuration = BuildConfiguration(
            ("UniEmu:TimeZone", "UTC"),
            ("UniEmu:Culture", "en-US"));

        var options = ApplicationGlobalizationOptions.Resolve(configuration);

        Assert.Equal("UTC", options.TimeZone);
        Assert.Equal("en-US", options.Culture);
    }

    [Fact]
    public void Apply_ConfiguresProcessTimeZoneAndDefaultCultures()
    {
        var configuration = BuildConfiguration(
            ("UniEmu:TimeZone", "UTC"),
            ("UniEmu:Culture", "en-US"));

        var options = ApplicationGlobalizationOptions.Resolve(configuration);

        ApplicationGlobalization.Apply(options);

        Assert.Equal("UTC", Environment.GetEnvironmentVariable("TZ"));
        Assert.Equal("en-US", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("en-US", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
    }

    public void Dispose()
    {
        ApplicationGlobalization.Apply(new ApplicationGlobalizationOptions(
            ApplicationGlobalizationOptions.DefaultTimeZone,
            originalThreadCulture.Name));
        Environment.SetEnvironmentVariable("TZ", originalTimeZone);
        TimeZoneInfo.ClearCachedData();
        CultureInfo.DefaultThreadCurrentCulture = originalCulture;
        CultureInfo.DefaultThreadCurrentUICulture = originalUiCulture;
        CultureInfo.CurrentCulture = originalThreadCulture;
        CultureInfo.CurrentUICulture = originalThreadUiCulture;
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
