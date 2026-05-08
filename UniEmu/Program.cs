using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Data;
using UniEmu.Features.CncPrograms;
using UniEmu.Features.Emulators;
using UniEmu.Features.Events;
using UniEmu.Features.Scripts;
using UniEmu.Features.Tags;
using UniEmu.Features.Telemetry;
using UniEmu.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<UniEmuDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("UniEmuDb")));
builder.Services.AddScoped<EmulatorService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<ScriptService>();
builder.Services.AddScoped<CncProgramService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<TelemetryService>();
builder.Services.AddScoped<EmulatorScheduleService>();
builder.Services.AddScoped<TagScriptExecutionService>();
builder.Services.AddSingleton<TelemetryValueGenerator>();
builder.Services.AddSingleton<TagRuntimeStateStore>();
builder.Services.AddHttpClient<TelemetryPacketSender>();
builder.Services.AddQuartz();
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
    await db.Database.EnsureCreatedAsync();
    await UniEmuSchemaUpdater.ApplyCompatibilityUpdatesAsync(db);
    await UniEmuSeeder.SeedAsync(db);

    if (!app.Configuration.GetValue<bool>("UniEmu:DisableRuntime"))
    {
        var scheduler = scope.ServiceProvider.GetRequiredService<EmulatorScheduleService>();
        await scheduler.ScheduleRunningEmulatorsAsync();
    }
}

if (!app.Configuration.GetValue<bool>("UniEmu:DisableStaticAssets"))
{
    app.UseDefaultFiles();
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

if (!app.Configuration.GetValue<bool>("UniEmu:DisableStaticAssets"))
{
    app.MapFallbackToFile("/index.html");
}

app.Run();
