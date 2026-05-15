using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UniEmu.Hosting;

namespace UniEmu.Tests.Hosting;

public sealed class UniEmuApplicationStartupTests
{
    [Fact]
    public void ShouldUseStaticAssetCompression_RequiresProductionAndEnabledStaticAssets()
    {
        var options = new UniEmuOptions
        {
            EnableStaticAssetCompression = true,
        };

        Assert.True(UniEmuApplicationStartup.ShouldUseStaticAssetCompression(
            new TestHostEnvironment(Environments.Production),
            options));
        Assert.False(UniEmuApplicationStartup.ShouldUseStaticAssetCompression(
            new TestHostEnvironment(Environments.Development),
            options));

        options.DisableStaticAssets = true;

        Assert.False(UniEmuApplicationStartup.ShouldUseStaticAssetCompression(
            new TestHostEnvironment(Environments.Production),
            options));
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/index.html", true)]
    [InlineData("/assets/app.js", true)]
    [InlineData("/emulators/42", true)]
    [InlineData("/api/emulators", false)]
    [InlineData("/hubs/runtime-updates", false)]
    public void ShouldCompressStaticAssetRequest_SkipsBackendEndpoints(
        string path,
        bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Equal(expected, UniEmuApplicationStartup.ShouldCompressStaticAssetRequest(context));
    }

    [Fact]
    public void ApplyStaticAssetCacheHeaders_SkipsCacheHeadersWhenCachingDisabled()
    {
        var context = new DefaultHttpContext();
        var options = new UniEmuOptions
        {
            EnableStaticAssetCaching = false,
        };

        UniEmuApplicationStartup.ApplyStaticAssetCacheHeaders(context.Response, "assets/app.js", options);

        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void ApplyStaticAssetCacheHeaders_CachesStaticAssetsForThirtyDays()
    {
        var context = new DefaultHttpContext();
        var options = new UniEmuOptions
        {
            EnableStaticAssetCaching = true,
        };

        UniEmuApplicationStartup.ApplyStaticAssetCacheHeaders(context.Response, "assets/app.js", options);

        Assert.Equal("public, max-age=2592000", context.Response.Headers.CacheControl);
    }

    [Fact]
    public void ApplyStaticAssetCacheHeaders_PreventsIndexHtmlFromStickingInBrowserCache()
    {
        var context = new DefaultHttpContext();
        var options = new UniEmuOptions
        {
            EnableStaticAssetCaching = true,
        };

        UniEmuApplicationStartup.ApplyStaticAssetCacheHeaders(context.Response, "index.html", options);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = nameof(UniEmuApplicationStartupTests);

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
