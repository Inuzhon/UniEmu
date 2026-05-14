using System.Diagnostics;
using System.Text;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Serilog;
using UniEmu.Hosting;
using UniEmu.Realtime;

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
builder.Host.ConfigureContainer<ContainerBuilder>(UniEmuBackendServiceRegistration.RegisterServices);

builder.Services
    .AddUniEmuOptions(builder.Configuration)
    .AddUniEmuWebApi()
    .AddUniEmuDatabase(builder.Configuration)
    .AddUniEmuHttpClients()
    .AddUniEmuRuntime(builder.Configuration);

var app = builder.Build();

await app.InitializeUniEmuDatabaseAsync();
app.PersistRuntimeStateOnShutdown();
app.UseUniEmuStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RuntimeUpdatesHub>("/hubs/runtime-updates");
app.MapUniEmuFallback();

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("Listening on port {Port}", backendPortOptions.Port));

await app.RunAsync();
