using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Common;
using UniEmu.Data;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting.Rest;

namespace UniEmu.Hosting;

public static class UniEmuServiceCollectionExtensions
{
    public static IServiceCollection AddUniEmuOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<UniEmuOptions>()
            .Bind(configuration.GetSection(UniEmuOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddUniEmuWebApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                UniEmuJson.Apply(options.JsonSerializerOptions);
            });

        services.AddOpenApi();
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                UniEmuJson.Apply(options.PayloadSerializerOptions);
            });
        services.AddMemoryCache();

        return services;
    }

    public static IServiceCollection AddUniEmuDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<UniEmuDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("UniEmuDb")));

        return services;
    }

    public static IServiceCollection AddUniEmuHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(TelemetryPacketSender), configure =>
        {
            configure.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        });

        services.AddHttpClient(nameof(TagScriptRestClient));

        return services;
    }

    public static IServiceCollection AddUniEmuRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddQuartz();
        var uniEmuOptions = configuration
            .GetSection(UniEmuOptions.SectionName)
            .Get<UniEmuOptions>() ?? new UniEmuOptions();

        if (!uniEmuOptions.DisableRuntime)
        {
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
        }

        return services;
    }
}
