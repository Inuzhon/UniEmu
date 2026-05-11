using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using UniEmu.Runtime.Scripting.Workspace;

namespace UniEmu.Runtime.Scripting.Services;

internal static class CsxSourceMapping
{
    public static CsxLocation? ToLocation(CsxRoslynContext context, Location location)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var mapped = location.GetMappedLineSpan();
        if (!string.IsNullOrWhiteSpace(mapped.Path))
        {
            return new CsxLocation(
                TagScriptPath.Normalize(mapped.Path),
                new CsxTextRange(
                    mapped.StartLinePosition.Line,
                    mapped.StartLinePosition.Character,
                    mapped.EndLinePosition.Line,
                    mapped.EndLinePosition.Character));
        }

        var span = location.SourceSpan;
        if (span.Start < context.EntryContentStart)
        {
            return null;
        }

        return new CsxLocation(
            context.EntryPath,
            ToEntryRange(context, span));
    }

    public static CsxTextRange ToEntryRange(CsxRoslynContext context, TextSpan expandedSpan)
    {
        var localStart = Math.Clamp(expandedSpan.Start - context.EntryContentStart, 0, context.EntryContent.Length);
        var localEnd = Math.Clamp(expandedSpan.End - context.EntryContentStart, localStart, context.EntryContent.Length);
        var sourceText = SourceText.From(context.EntryContent);
        var lineSpan = sourceText.Lines.GetLinePositionSpan(TextSpan.FromBounds(localStart, localEnd));

        return new CsxTextRange(
            lineSpan.Start.Line,
            lineSpan.Start.Character,
            lineSpan.End.Line,
            lineSpan.End.Character);
    }

    public static bool IsEntryLocation(CsxRoslynContext context, Location location)
    {
        if (!location.IsInSource)
        {
            return false;
        }

        var mapped = location.GetMappedLineSpan();
        if (!string.IsNullOrWhiteSpace(mapped.Path))
        {
            return string.Equals(TagScriptPath.Normalize(mapped.Path), context.EntryPath, StringComparison.OrdinalIgnoreCase);
        }

        return location.SourceSpan.Start >= context.EntryContentStart;
    }
}
