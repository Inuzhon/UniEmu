using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxFormattingService
{
    private static readonly Regex s_leadingLoadBlockWithBlankLine = new(
        @"\A(?<loads>(?:\s*#\s*load\s+""[^""]+""\s*(?:\r?\n|$))+)(?:\r?\n)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex s_leadingLoadBlock = new(
        @"\A(?<loads>(?:\s*#\s*load\s+""[^""]+""\s*(?:\r?\n|$))+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<CsxTextEdit>> FormatDocumentAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var formatted = await FormatAsync(content, cancellationToken);
        return
        [
            new CsxTextEdit(WholeDocumentRange(content), formatted),
        ];
    }

    public async Task<IReadOnlyList<CsxTextEdit>> FormatRangeAsync(
        string content,
        CsxTextRange range,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var formatted = await FormatAsync(content, cancellationToken);
        return
        [
            new CsxTextEdit(WholeDocumentRange(content), formatted),
        ];
    }

    private static Task<string> FormatAsync(string content, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(content, options: CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot(cancellationToken);
        using var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(root, workspace, cancellationToken: cancellationToken);
        var formatted = PreserveBlankLineAfterLeadingLoads(content, formattedRoot.ToFullString());
        return Task.FromResult(formatted);
    }

    private static string PreserveBlankLineAfterLeadingLoads(string original, string formatted)
    {
        if (!s_leadingLoadBlockWithBlankLine.IsMatch(original))
        {
            return formatted;
        }

        var match = s_leadingLoadBlock.Match(formatted);
        if (!match.Success)
        {
            return formatted;
        }

        var newline = formatted.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var loadBlock = match.Groups["loads"].Value.TrimEnd('\r', '\n');
        var rest = formatted[match.Length..].TrimStart('\r', '\n');
        return loadBlock + newline + newline + rest;
    }

    private static CsxTextRange WholeDocumentRange(string content)
    {
        var text = SourceText.From(content);
        if (text.Lines.Count == 0)
        {
            return new CsxTextRange(0, 0, 0, 0);
        }

        var lastLine = text.Lines[^1];
        return new CsxTextRange(0, 0, text.Lines.Count - 1, lastLine.End - lastLine.Start);
    }
}
