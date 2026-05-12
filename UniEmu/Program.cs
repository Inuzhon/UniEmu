using System.Diagnostics;
using System.Reflection;
using System.Text;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using UniEmu.Common;
using UniEmu.Data;
using UniEmu.Features.CncPrograms;
using UniEmu.Features.Emulators;
using UniEmu.Features.Events;
using UniEmu.Features.Scripts;
using UniEmu.Features.Tags;
using UniEmu.Features.Telemetry;
using UniEmu.Hosting;
using UniEmu.Realtime;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Runtime.Scripting.Services;
using UniEmu.Runtime.Scripting.Workspace;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Activity.DefaultIdFormat = ActivityIdFormat.W3C;

Directory.SetCurrentDirectory(AppContext.BaseDirectory ?? throw new NullReferenceException("Current dir not found!"));

var builder = WebApplication.CreateBuilder(args);
var globalizationOptions = ApplicationGlobalizationOptions.Resolve(builder.Configuration);
ApplicationGlobalization.Apply(globalizationOptions);
var backendPortOptions = BackendPortOptions.Resolve(builder.Configuration);

builder.WebHost.UseUrls(backendPortOptions.HttpUrl);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());
builder.Host.ConfigureContainer<ContainerBuilder>(RegisterUniEmuServices);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        UniEmuJson.Apply(options.JsonSerializerOptions);
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        UniEmuJson.Apply(options.PayloadSerializerOptions);
    });
builder.Services.AddDbContext<UniEmuDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("UniEmuDb")));
builder.Services.AddMemoryCache();
builder.Services.AddQuartz();

builder.Services.AddHttpClient(nameof(TelemetryPacketSender), configure =>
{
    configure.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler()
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

if (!builder.Configuration.GetValue<bool>("UniEmu:DisableRuntime"))
{
    builder.Services.AddQuartzHostedService(options =>
    {
        options.WaitForJobsToComplete = true;
    });
}

var app = builder.Build();

if (!app.Configuration.GetValue<bool>("UniEmu:SkipStartupDatabase"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<UniEmuDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("UniEmu:SeedData"))
        await UniEmuSeeder.SeedAsync(db);

    if (!app.Configuration.GetValue<bool>("UniEmu:DisableRuntime"))
    {
        var statePersistence = scope.ServiceProvider.GetRequiredService<TagRuntimeStatePersistenceService>();
        await statePersistence.HydrateFromTagPreviewsAsync();

        var scheduler = scope.ServiceProvider.GetRequiredService<EmulatorScheduleService>();
        await scheduler.ScheduleRunningEmulatorsAsync();
    }
}

if (!app.Configuration.GetValue<bool>("UniEmu:DisableRuntime"))
{
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        using var scope = app.Services.CreateScope();
        var statePersistence = scope.ServiceProvider.GetRequiredService<TagRuntimeStatePersistenceService>();
        statePersistence.PersistToTagPreviewsAsync().GetAwaiter().GetResult();
    });
}

if (!app.Configuration.GetValue<bool>("UniEmu:DisableStaticAssets"))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapStaticAssets();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<RuntimeUpdatesHub>("/hubs/runtime-updates");

if (!app.Configuration.GetValue<bool>("UniEmu:DisableStaticAssets"))
{
    app.MapFallbackToFile("/index.html");
}

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("Listening on port {Port}", backendPortOptions.Port));

await app.RunAsync();

static void RegisterUniEmuServices(ContainerBuilder container)
{
    container.RegisterType<EmulatorService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<TagService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<ScriptService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<CncProgramService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<EventService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<TelemetryService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<CachedUniEmuDataService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<RuntimeUpdateService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<SignalRRuntimeUpdateBroadcaster>().As<IRuntimeUpdateBroadcaster>().InstancePerLifetimeScope();
    container.RegisterType<EmulatorScheduleService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<TagScriptExecutionService>().AsSelf().InstancePerLifetimeScope();
    container.RegisterType<TagRuntimeStatePersistenceService>().AsSelf().InstancePerLifetimeScope();

    container.RegisterType<TelemetryValueGenerator>().AsSelf().SingleInstance();
    container.RegisterType<TagRuntimeStateStore>().AsSelf().SingleInstance();
    container.RegisterType<CompiledTagScriptCache>().AsSelf().SingleInstance();
    container.RegisterType<CsxScriptEnvironment>().AsSelf().SingleInstance();
    container.RegisterType<CsxLoadedScriptExpander>().AsSelf().SingleInstance();
    container.RegisterType<CsxScriptDirectiveValidator>().AsSelf().SingleInstance();
    container.RegisterType<CsxScriptSecurityValidator>().AsSelf().SingleInstance();
    container.RegisterType<CsxRoslynContextFactory>().AsSelf().SingleInstance();
    container.RegisterType<CsxDiagnosticsService>().AsSelf().SingleInstance();
    container.RegisterType<CsxCompletionService>().AsSelf().SingleInstance();
    container.RegisterType<CsxHoverService>().AsSelf().SingleInstance();
    container.RegisterType<CsxSignatureHelpService>().AsSelf().SingleInstance();
    container.RegisterType<CsxLanguageService>().AsSelf().SingleInstance();
    container.RegisterType<CsxIntellisenseService>().AsSelf().InstancePerLifetimeScope();

    container.RegisterType<TelemetryPacketSender>().AsSelf().InstancePerDependency();
}
