using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniEmu.Common;

namespace UniEmu.Runtime.Scripting;

public static class CsxLspEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = UniEmuJson.Options;

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
            var session = new CsxLspWebSocketSession(
                webSocket,
                context.RequestServices.GetRequiredService<CsxDocumentStore>(),
                context.RequestServices.GetRequiredService<CsxLanguageService>(),
                context.RequestServices.GetRequiredService<ILogger<CsxLspWebSocketSession>>());

            await session.RunAsync(context.RequestAborted);
        });
    }

    private sealed class CsxLspWebSocketSession(
        WebSocket webSocket,
        CsxDocumentStore documents,
        CsxLanguageService language,
        ILogger<CsxLspWebSocketSession> logger)
    {
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(cancellationToken);
                if (message is null)
                {
                    break;
                }

                await HandleMessageAsync(message, cancellationToken);
            }
        }

        private async Task<JsonObject?> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            return JsonNode.Parse(json)?.AsObject();
        }

        private async Task HandleMessageAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var method = message["method"]?.GetValue<string>();
            var id = message["id"]?.DeepClone();

            try
            {
                switch (method)
                {
                    case "initialize":
                        await SendResponseAsync(id, InitializeResult(), cancellationToken);
                        break;
                    case "shutdown":
                        await SendResponseAsync(id, null, cancellationToken);
                        break;
                    case "exit":
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "LSP exit", cancellationToken);
                        break;
                    case "textDocument/didOpen":
                        await HandleDidOpenAsync(message, cancellationToken);
                        break;
                    case "textDocument/didChange":
                        await HandleDidChangeAsync(message, cancellationToken);
                        break;
                    case "textDocument/didClose":
                        await HandleDidCloseAsync(message, cancellationToken);
                        break;
                    case "textDocument/completion":
                        await SendResponseAsync(id, await HandleCompletionAsync(message, cancellationToken), cancellationToken);
                        break;
                    default:
                        if (id is not null)
                        {
                            await SendResponseAsync(id, null, cancellationToken);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to handle CSX LSP method {Method}", method);
                if (id is not null)
                {
                    await SendErrorAsync(id, -32603, ex.Message, cancellationToken);
                }
            }
        }

        private async Task HandleDidOpenAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var document = message["params"]?["textDocument"]?.AsObject();
            if (document is null)
            {
                return;
            }

            var uri = document["uri"]?.GetValue<string>();
            var text = document["text"]?.GetValue<string>() ?? string.Empty;
            var version = document["version"]?.GetValue<int?>();
            if (uri is null)
            {
                return;
            }

            documents.Open(uri, text, version);
            await PublishDiagnosticsAsync(uri, text, version, cancellationToken);
        }

        private async Task HandleDidChangeAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var textDocument = message["params"]?["textDocument"]?.AsObject();
            var changes = message["params"]?["contentChanges"]?.AsArray();
            var uri = textDocument?["uri"]?.GetValue<string>();
            var version = textDocument?["version"]?.GetValue<int?>();
            var text = changes?.LastOrDefault()?["text"]?.GetValue<string>();
            if (uri is null || text is null)
            {
                return;
            }

            documents.Update(uri, text, version);
            await PublishDiagnosticsAsync(uri, text, version, cancellationToken);
        }

        private async Task HandleDidCloseAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var uri = message["params"]?["textDocument"]?["uri"]?.GetValue<string>();
            if (uri is null)
            {
                return;
            }

            documents.Close(uri);
            await SendNotificationAsync("textDocument/publishDiagnostics", new JsonObject
            {
                ["uri"] = uri,
                ["diagnostics"] = new JsonArray(),
            }, cancellationToken);
        }

        private async Task<JsonObject> HandleCompletionAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var uri = message["params"]?["textDocument"]?["uri"]?.GetValue<string>();
            if (uri is null || !documents.TryGet(uri, out var document))
            {
                return CompletionList([]);
            }

            var line = message["params"]?["position"]?["line"]?.GetValue<int>() ?? 0;
            var character = message["params"]?["position"]?["character"]?.GetValue<int>() ?? 0;
            var visibleScripts = await documents.LoadVisibleScriptsAsync(document.Uri, cancellationToken);
            var completions = language.GetCompletions(
                document.Uri,
                document.Text,
                ToOffset(document.Text, line, character),
                visibleScripts,
                typeof(TagScriptGlobals));

            return CompletionList(completions.Select(item => new JsonObject
            {
                ["label"] = item.Label,
                ["insertText"] = item.InsertText,
                ["sortText"] = item.SortText,
                ["filterText"] = item.FilterText,
                ["kind"] = 2,
            }));
        }

        private async Task PublishDiagnosticsAsync(
            string uri,
            string text,
            int? version,
            CancellationToken cancellationToken)
        {
            var visibleScripts = await documents.LoadVisibleScriptsAsync(uri, cancellationToken);
            var result = language.Analyze(uri, text, visibleScripts, typeof(TagScriptGlobals));
            var diagnostics = new JsonArray(result.Diagnostics.Select(ToLspDiagnostic).ToArray<JsonNode?>());
            await SendNotificationAsync("textDocument/publishDiagnostics", new JsonObject
            {
                ["uri"] = uri,
                ["version"] = version,
                ["diagnostics"] = diagnostics,
            }, cancellationToken);
        }

        private async Task SendResponseAsync(JsonNode? id, JsonNode? result, CancellationToken cancellationToken)
        {
            await SendAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = result?.DeepClone(),
            }, cancellationToken);
        }

        private async Task SendErrorAsync(JsonNode id, int code, string message, CancellationToken cancellationToken)
        {
            await SendAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.DeepClone(),
                ["error"] = new JsonObject
                {
                    ["code"] = code,
                    ["message"] = message,
                },
            }, cancellationToken);
        }

        private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            await SendAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters,
            }, cancellationToken);
        }

        private async Task SendAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        private static JsonObject InitializeResult() => new()
        {
            ["capabilities"] = new JsonObject
            {
                ["textDocumentSync"] = new JsonObject
                {
                    ["openClose"] = true,
                    ["change"] = 1,
                    ["save"] = new JsonObject
                    {
                        ["includeText"] = true,
                    },
                },
                ["completionProvider"] = new JsonObject
                {
                    ["resolveProvider"] = false,
                    ["triggerCharacters"] = new JsonArray(".", "#", "\""),
                },
            },
        };

        private static JsonObject CompletionList(IEnumerable<JsonObject> items)
        {
            return new JsonObject
            {
                ["isIncomplete"] = false,
                ["items"] = new JsonArray(items.ToArray<JsonNode?>()),
            };
        }

        private static JsonObject ToLspDiagnostic(CsxDiagnostic diagnostic)
        {
            return new JsonObject
            {
                ["code"] = diagnostic.Code,
                ["source"] = "UniEmu CSX",
                ["message"] = diagnostic.Message,
                ["severity"] = (int)diagnostic.Severity,
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject
                    {
                        ["line"] = diagnostic.StartLine,
                        ["character"] = diagnostic.StartCharacter,
                    },
                    ["end"] = new JsonObject
                    {
                        ["line"] = diagnostic.EndLine,
                        ["character"] = diagnostic.EndCharacter,
                    },
                },
            };
        }

        private static int ToOffset(string text, int line, int character)
        {
            var currentLine = 0;
            var offset = 0;
            while (currentLine < line && offset < text.Length)
            {
                var next = text.IndexOf('\n', offset);
                if (next < 0)
                {
                    return text.Length;
                }

                offset = next + 1;
                currentLine++;
            }

            return Math.Clamp(offset + character, 0, text.Length);
        }
    }
}
