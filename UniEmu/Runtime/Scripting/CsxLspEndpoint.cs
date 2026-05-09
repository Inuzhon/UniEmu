using Nerdbank.Streams;
using OmniSharp.Extensions.LanguageServer.Server;

namespace UniEmu.Runtime.Scripting;

public static class CsxLspEndpoint
{
    public static void MapCsxLsp(this WebApplication app)
    {
        app.UseWebSockets();
        app.Map("/csx-lsp", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await using var stream = webSocket.AsStream();

            var server = await LanguageServer.From(options => options
                .WithInput(stream)
                .WithOutput(stream)
                .ConfigureLogging(logging => logging
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug))
                .WithHandler<CsxTextDocumentSyncHandler>()
                .WithHandler<CsxCompletionHandler>()
                .WithServices(services =>
                {
                    services.AddSingleton(context.RequestServices.GetRequiredService<CsxDocumentStore>());
                    services.AddSingleton(context.RequestServices.GetRequiredService<CsxLanguageService>());
                    services.AddSingleton(context.RequestServices.GetRequiredService<IServiceScopeFactory>());
                }));

            await server.WaitForExit;
        });
    }
}
