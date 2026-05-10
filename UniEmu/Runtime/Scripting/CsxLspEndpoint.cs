using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using Nerdbank.Streams;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace UniEmu.Runtime.Scripting;

public static class CsxLspEndpoint
{
    private static readonly byte[] HeaderSeparator = "\r\n\r\n"u8.ToArray();

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

            var logger = context.RequestServices.GetRequiredService<Serilog.ILogger>();

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var duplexPipe = new DuplexPipe(clientToServer.Reader, serverToClient.Writer);
            await using var stream = duplexPipe.AsStream();

            var incoming = PumpWebSocketToLanguageServerAsync(webSocket, clientToServer.Writer, cancellation.Token);
            var outgoing = PumpLanguageServerToWebSocketAsync(serverToClient.Reader, webSocket, cancellation.Token);

            try
            {
                var server = await LanguageServer.From(options => options
                    .WithInput(stream)
                    .WithOutput(stream)
                    .ConfigureLogging(logging => logging
                        .AddSerilog(logger, dispose: true)
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
            }
            finally
            {
                await StopPumpsAsync(cancellation, clientToServer, serverToClient, incoming, outgoing);
            }
        });
    }

    private static async Task PumpWebSocketToLanguageServerAsync(
        WebSocket webSocket,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var body = message.ToArray();
                var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

                await writer.WriteAsync(header, cancellationToken);
                await writer.WriteAsync(body, cancellationToken);
                var flush = await writer.FlushAsync(cancellationToken);
                if (flush.IsCompleted || flush.IsCanceled)
                {
                    return;
                }
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private static async Task PumpLanguageServerToWebSocketAsync(
        PipeReader reader,
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var pending = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                foreach (var segment in result.Buffer)
                {
                    pending.AddRange(segment.Span.ToArray());
                }

                reader.AdvanceTo(result.Buffer.End);

                while (TryTakeLspMessage(pending, out var body))
                {
                    if (webSocket.State != WebSocketState.Open)
                    {
                        return;
                    }

                    await webSocket.SendAsync(body, WebSocketMessageType.Text, true, cancellationToken);
                }

                if (result.IsCompleted)
                {
                    return;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private static bool TryTakeLspMessage(List<byte> pending, out byte[] body)
    {
        body = [];

        var separatorIndex = IndexOf(pending, HeaderSeparator);
        if (separatorIndex < 0)
        {
            return false;
        }

        var header = Encoding.ASCII.GetString(pending.GetRange(0, separatorIndex).ToArray());
        var contentLength = ParseContentLength(header);
        if (contentLength is null)
        {
            pending.RemoveRange(0, separatorIndex + HeaderSeparator.Length);
            return false;
        }

        var bodyStart = separatorIndex + HeaderSeparator.Length;
        if (pending.Count - bodyStart < contentLength.Value)
        {
            return false;
        }

        body = pending.GetRange(bodyStart, contentLength.Value).ToArray();
        pending.RemoveRange(0, bodyStart + contentLength.Value);
        return true;
    }

    private static int? ParseContentLength(string header)
    {
        foreach (var line in header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (!string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (int.TryParse(value, out var length) && length >= 0)
            {
                return length;
            }
        }

        return null;
    }

    private static int IndexOf(List<byte> buffer, byte[] value)
    {
        for (var i = 0; i <= buffer.Count - value.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < value.Length; j++)
            {
                if (buffer[i + j] == value[j])
                {
                    continue;
                }

                matched = false;
                break;
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task StopPumpsAsync(
        CancellationTokenSource cancellation,
        Pipe clientToServer,
        Pipe serverToClient,
        Task incoming,
        Task outgoing)
    {
        await cancellation.CancelAsync();

        await clientToServer.Writer.CompleteAsync();
        await serverToClient.Reader.CompleteAsync();

        try
        {
            await Task.WhenAll(incoming, outgoing);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;

        public PipeWriter Output { get; } = output;
    }
}

public class LSPLog
{
}
