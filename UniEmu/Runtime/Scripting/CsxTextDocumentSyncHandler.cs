using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxTextDocumentSyncHandler(
    CsxDocumentStore documents,
    CsxLanguageService language,
    ILanguageServerFacade server,
    ILogger<CsxTextDocumentSyncHandler> logger) : TextDocumentSyncHandlerBase
{
    private static readonly TextDocumentSelector Selector = new(
        new TextDocumentFilter
        {
            Language = "csx",
            Pattern = "**/*.csx",
        });

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        documents.Open(notification.TextDocument.Uri.ToString(), notification.TextDocument.Text, notification.TextDocument.Version);
        await PublishDiagnosticsAsync(notification.TextDocument.Uri, notification.TextDocument.Text, notification.TextDocument.Version, token);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        var text = notification.ContentChanges.LastOrDefault()?.Text;
        if (text is null)
        {
            return Unit.Value;
        }

        documents.Update(notification.TextDocument.Uri.ToString(), text, notification.TextDocument.Version);
        await PublishDiagnosticsAsync(notification.TextDocument.Uri, text, notification.TextDocument.Version, token);
        return Unit.Value;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
        documents.Close(notification.TextDocument.Uri.ToString());
        server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = notification.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>(),
        });

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "csx");

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = Selector,
            Change = Change,
            Save = new SaveOptions { IncludeText = true },
        };

    private async Task PublishDiagnosticsAsync(DocumentUri uri, string text, int? version, CancellationToken cancellationToken)
    {
        try
        {
            var visibleScripts = await documents.LoadVisibleScriptsAsync(uri.ToString(), cancellationToken);
            var result = language.Analyze(uri.ToString(), text, visibleScripts, typeof(TagScriptGlobals));
            var diagnostics = result.Diagnostics
                .Select(ToLspDiagnostic)
                .ToArray();

            server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Version = version,
                Diagnostics = new Container<Diagnostic>(diagnostics),
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish CSX diagnostics for {Uri}", uri);
        }
    }

    private static Diagnostic ToLspDiagnostic(CsxDiagnostic diagnostic)
    {
        return new Diagnostic
        {
            Code = diagnostic.Code,
            Source = "UniEmu CSX",
            Message = diagnostic.Message,
            Severity = diagnostic.Severity switch
            {
                CsxDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                CsxDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                CsxDiagnosticSeverity.Information => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Hint,
            },
            Range = new Range(
                new Position(diagnostic.StartLine, diagnostic.StartCharacter),
                new Position(diagnostic.EndLine, diagnostic.EndCharacter)),
        };
    }
}
