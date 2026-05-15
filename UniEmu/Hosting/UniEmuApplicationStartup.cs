using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using UniEmu.Data;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Hosting;

public static class UniEmuApplicationStartup
{
    private static readonly TimeSpan StaticAssetCacheDuration = TimeSpan.FromDays(30);

    public static async Task InitializeUniEmuDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (options.SkipStartupDatabase)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UniEmuDbContext>();
        await db.Database.MigrateAsync();

        if (options.SeedData)
        {
            await UniEmuSeeder.SeedAsync(db);
        }

        if (!options.DisableRuntime)
        {
            var statePersistence = scope.ServiceProvider.GetRequiredService<TagRuntimeStatePersistenceService>();
            await statePersistence.HydrateFromTagPreviewsAsync();

            var scheduler = scope.ServiceProvider.GetRequiredService<EmulatorScheduleService>();
            await scheduler.ScheduleRunningEmulatorsAsync();
        }
    }

    public static void PersistRuntimeStateOnShutdown(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (options.DisableRuntime)
        {
            return;
        }

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            using var scope = app.Services.CreateScope();
            var statePersistence = scope.ServiceProvider.GetRequiredService<TagRuntimeStatePersistenceService>();
            statePersistence.PersistToTagPreviewsAsync().GetAwaiter().GetResult();
        });
    }

    public static void UseUniEmuStaticAssets(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (options.DisableStaticAssets)
        {
            return;
        }

        var staticFileOptions = CreateStaticFileOptions(options);

        app.UseDefaultFiles();
        app.UseStaticFiles(staticFileOptions);
        app.MapStaticAssets();
    }

    public static void UseUniEmuStaticAssetCompression(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (!ShouldUseStaticAssetCompression(app.Environment, options))
        {
            return;
        }

        app.UseWhen(ShouldCompressStaticAssetRequest, branch =>
        {
            branch.UseResponseCompression();
        });
    }

    public static void MapUniEmuFallback(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (!options.DisableStaticAssets)
        {
            app.MapFallbackToFile("/index.html", CreateStaticFileOptions(options));
        }
    }

    internal static void ApplyStaticAssetCacheHeaders(
        HttpResponse response,
        string? fileName,
        UniEmuOptions options)
    {
        if (!options.EnableStaticAssetCaching)
        {
            return;
        }

        response.Headers.CacheControl = string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase)
            ? "no-cache"
            : new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = StaticAssetCacheDuration,
            }.ToString();
    }

    internal static bool ShouldUseStaticAssetCompression(
        IHostEnvironment environment,
        UniEmuOptions options)
    {
        return !options.DisableStaticAssets &&
            options.EnableStaticAssetCompression &&
            environment.IsProduction();
    }

    internal static bool ShouldCompressStaticAssetRequest(HttpContext context)
    {
        return !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
    }

    private static StaticFileOptions CreateStaticFileOptions(UniEmuOptions options)
    {
        return new StaticFileOptions
        {
            OnPrepareResponse = context =>
                ApplyStaticAssetCacheHeaders(context.Context.Response, context.File.Name, options),
        };
    }
}
