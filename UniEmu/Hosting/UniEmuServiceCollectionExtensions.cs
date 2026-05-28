using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Quartz;
using UniEmu.Common;
using UniEmu.Data;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting.Rest;

namespace UniEmu.Hosting;

public static class UniEmuServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUniEmuOptions(IConfiguration configuration)
        {
            services.AddOptions<UniEmuOptions>()
                .Bind(configuration.GetSection(UniEmuOptions.SectionName));

            return services;
        }

        public IServiceCollection AddUniEmuWebApi()
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

        public IServiceCollection AddUniEmuStaticAssetCompression()
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            return services;
        }

        public IServiceCollection AddUniEmuDatabase(IConfiguration configuration)
        {
            services.AddDbContext<UniEmuDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("UniEmuDb")));

            return services;
        }

        public IServiceCollection AddUniEmuHttpClients()
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

        public IServiceCollection AddUniEmuRuntime(IConfiguration configuration)
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
}
