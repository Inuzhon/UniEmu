using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxCompletionHandler(
    CsxDocumentStore documents,
    CsxLanguageService language,
    ILogger<CsxCompletionHandler> logger) : ICompletionHandler
{
    private static readonly TextDocumentSelector Selector = new(
        new TextDocumentFilter
        {
            Language = "csx",
            Pattern = "**/*.csx",
        });

    private CompletionCapability? capability;

    public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToString(), out var document))
        {
            return new CompletionList();
        }

        try
        {
            var visibleScripts = await documents.LoadVisibleScriptsAsync(document.Uri, cancellationToken);
            var position = ToOffset(document.Text, request.Position.Line, request.Position.Character);
            var completions = language.GetCompletions(
                document.Uri,
                document.Text,
                position,
                visibleScripts,
                typeof(TagScriptGlobals));

            return new CompletionList(
                completions.Select(item => new CompletionItem
                {
                    Label = item.Label,
                    InsertText = item.InsertText,
                    SortText = item.SortText,
                    FilterText = item.FilterText,
                    Kind = CompletionItemKind.Method,
                }),
                isIncomplete: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to provide CSX completions for {Uri}", document.Uri);
            return new CompletionList();
        }
    }

    public CompletionRegistrationOptions GetRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = Selector,
            ResolveProvider = false,
            TriggerCharacters = new Container<string>(".", "#", "\""),
        };

    public void SetCapability(CompletionCapability capability)
    {
        this.capability = capability;
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
