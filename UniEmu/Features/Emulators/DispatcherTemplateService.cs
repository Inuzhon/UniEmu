using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;

namespace UniEmu.Features.Emulators;

/// <summary>
/// Creates Universal dispatcher protocol XML templates for emulator tags.
/// </summary>
public sealed class DispatcherTemplateService(UniEmuDbContext db)
{
    /// <summary>
    /// Creates a Universal XML template file for an emulator.
    /// </summary>
    /// <param name="emulatorId">Emulator identifier.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    /// <returns>Generated XML file or <see langword="null"/> when emulator is missing.</returns>
    public async Task<DispatcherTemplateFile?> CreateAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var emulator = await db.Emulators
            .AsNoTracking()
            .Where(e => e.Id == emulatorId)
            .Select(e => new
            {
                e.ProtocolId,
                Tags = e.Tags
                    .OrderBy(t => t.Name)
                    .Select(t => new
                    {
                        t.Key,
                        t.Type,
                        t.SpecialParameter,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (emulator is null)
        {
            return null;
        }

        var root = new XElement(
            "ArrayOfUniversalItemXml",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            emulator.Tags.Select(tag => new XElement(
                "UniversalItemXml",
                new XElement("MatchingXmlList"),
                new XElement("Name", tag.Key),
                new XElement("UniversalParam", tag.Key),
                new XElement("SpecialParamNum", GetSpecialParameterNumber(tag.SpecialParameter)),
                new XElement("DataTypeNum", GetDataTypeNumber(tag.Type)),
                new XElement("ExecutionUPStatusNum", 0))));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var fileName = $"Universal_template_machineID_{emulator.ProtocolId}.xml";
        return new DispatcherTemplateFile(fileName, WriteXml(document));
    }

    private static int GetSpecialParameterNumber(string? specialParameter)
    {
        if (string.IsNullOrWhiteSpace(specialParameter))
        {
            return (int)SpecialParameter.None;
        }

        return (int)UniEmuJson.EnumValue<SpecialParameter>(specialParameter);
    }

    private static int GetDataTypeNumber(string tagType)
    {
        return UniEmuJson.EnumValue<TagType>(tagType) switch
        {
            TagType.Bool => 0,
            TagType.Int or TagType.Double => 1,
            TagType.String => 2,
            _ => 2,
        };
    }

    private static string WriteXml(XDocument document)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(
                   new Utf8StringWriter(builder),
                   new XmlWriterSettings
                   {
                       Encoding = Encoding.UTF8,
                       Indent = true,
                       OmitXmlDeclaration = false,
                   }))
        {
            document.Save(writer);
        }

        return builder.ToString();
    }

    private sealed class Utf8StringWriter(StringBuilder builder) : StringWriter(builder)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}

/// <summary>
/// Generated dispatcher template file payload.
/// </summary>
/// <param name="FileName">Suggested download filename.</param>
/// <param name="Content">XML content.</param>
public sealed record DispatcherTemplateFile(string FileName, string Content);
