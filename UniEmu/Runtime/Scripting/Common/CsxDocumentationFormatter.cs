using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime.Scripting.Common;

public static class CsxDocumentationFormatter
{
    public static string? FromXml(ISymbol symbol)
    {
        var documentation = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(documentation))
        {
            return null;
        }

        return Regex.Replace(documentation, "<.*?>", " ")
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    public static string? FromTaggedParts(ImmutableArray<TaggedText> parts)
    {
        if (parts.IsDefaultOrEmpty)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Tag == TextTags.LineBreak)
            {
                builder.AppendLine();
            }
            else
            {
                builder.Append(part.Text);
            }
        }

        var value = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
