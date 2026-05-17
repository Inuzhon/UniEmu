using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
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

        try
        {
            var document = XDocument.Parse(documentation, LoadOptions.PreserveWhitespace);
            var builder = new StringBuilder();

            foreach (var node in document.Root?.Nodes() ?? Enumerable.Empty<XNode>())
            {
                AppendXmlDocumentationText(node, builder);
            }

            return NormalizeDocumentation(builder.ToString());
        }
        catch (XmlException)
        {
            return NormalizeDocumentation(Regex.Replace(documentation, "<.*?>", " "));
        }
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

    private static void AppendXmlDocumentationText(XNode node, StringBuilder builder)
    {
        switch (node)
        {
            case XText text:
                builder.Append(text.Value);
                break;
            case XElement element:
                AppendXmlDocumentationElementText(element, builder);
                break;
        }
    }

    private static void AppendXmlDocumentationElementText(XElement element, StringBuilder builder)
    {
        if (TryAppendXmlDocumentationReference(element, builder))
        {
            return;
        }

        foreach (var node in element.Nodes())
        {
            AppendXmlDocumentationText(node, builder);
        }

        if (IsBlockDocumentationElement(element))
        {
            builder.Append(' ');
        }
    }

    private static bool TryAppendXmlDocumentationReference(XElement element, StringBuilder builder)
    {
        if (element.Name.LocalName is "see" or "seealso"
            && element.Attribute("langword")?.Value is { Length: > 0 } langword)
        {
            builder.Append(langword);
            return true;
        }

        if (element.Name.LocalName is "paramref" or "typeparamref"
            && element.Attribute("name")?.Value is { Length: > 0 } parameterName)
        {
            builder.Append(parameterName);
            return true;
        }

        return false;
    }

    private static bool IsBlockDocumentationElement(XElement element)
    {
        return element.Name.LocalName is "summary"
            or "remarks"
            or "param"
            or "typeparam"
            or "returns"
            or "value"
            or "exception"
            or "example";
    }

    private static string? NormalizeDocumentation(string value)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
