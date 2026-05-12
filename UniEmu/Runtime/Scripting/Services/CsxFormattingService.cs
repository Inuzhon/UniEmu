using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions;

namespace UniEmu.Runtime.Scripting.Services;

public sealed class CsxFormattingService
{
    private static readonly Regex s_leadingLoadBlockWithBlankLine = new(
        @"\A(?<loads>(?:\s*#\s*load\s+""[^""]+""\s*(?:\r?\n|$))+)(?:\r?\n)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex s_leadingLoadBlock = new(
        @"\A(?<loads>(?:\s*#\s*load\s+""[^""]+""\s*(?:\r?\n|$))+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<IReadOnlyList<CsxTextEdit>> FormatDocumentAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var formatted = Format(content);
        return Task.FromResult<IReadOnlyList<CsxTextEdit>>(
        [
            new CsxTextEdit(WholeDocumentRange(content), formatted),
        ]);
    }

    public Task<IReadOnlyList<CsxTextEdit>> FormatRangeAsync(
        string content,
        CsxTextRange range,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var formatted = Format(content);
        return Task.FromResult<IReadOnlyList<CsxTextEdit>>(
        [
            new CsxTextEdit(WholeDocumentRange(content), formatted),
        ]);
    }

    private static string Format(string content)
    {
        var tree = CSharpSyntaxTree.ParseText(content, options: CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot();
        var formatted = root.NormalizeWhitespace().ToFullString();
        return PreserveBlankLineAfterLeadingLoads(content, formatted);
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
