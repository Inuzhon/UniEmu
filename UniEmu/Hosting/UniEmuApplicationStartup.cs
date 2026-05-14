using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniEmu.Data;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Hosting;

public static class UniEmuApplicationStartup
{
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

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapStaticAssets();
    }

    public static void MapUniEmuFallback(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<UniEmuOptions>>().Value;

        if (!options.DisableStaticAssets)
        {
            app.MapFallbackToFile("/index.html");
        }
    }
}
