using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Emulators;

namespace UniEmu.Tests.Features.Emulators;

public sealed class DispatcherTemplateServiceTests
{
    [Fact]
    public async Task CreateAsync_GeneratesUniversalXmlTemplateFromEmulatorTags()
    {
        await using var fixture = await DispatcherTemplateDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new DispatcherTemplateService(db);

        var template = await service.CreateAsync("em-1", CancellationToken.None);

        Assert.NotNull(template);
        Assert.Equal("Universal_template_machineID_42.xml", template.FileName);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", template.Content);

        var document = XDocument.Parse(template.Content);
        var root = Assert.IsType<XElement>(document.Root);
        Assert.Equal("ArrayOfUniversalItemXml", root.Name.LocalName);
        Assert.Equal("http://www.w3.org/2001/XMLSchema-instance", root.Attribute(XNamespace.Xmlns + "xsi")?.Value);
        Assert.Equal("http://www.w3.org/2001/XMLSchema", root.Attribute(XNamespace.Xmlns + "xsd")?.Value);

        var items = root.Elements("UniversalItemXml").ToList();
        Assert.Equal(3, items.Count);
        var itemsByName = items.ToDictionary(item => item.Element("Name")?.Value ?? string.Empty);

        AssertUniversalItem(itemsByName["Power"], "Power", "PowerOn", "0", "0");
        AssertUniversalItem(itemsByName["Feed"], "Feed", "FeedRate", "4", "1");
        AssertUniversalItem(itemsByName["Program"], "Program", "ProgramName", "1", "2");
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await DispatcherTemplateDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new DispatcherTemplateService(db);

        var template = await service.CreateAsync("missing", CancellationToken.None);

        Assert.Null(template);
    }

    private static void AssertUniversalItem(
        XElement item,
        string name,
        string key,
        string specialParameter,
        string dataType)
    {
        Assert.NotNull(item.Element("MatchingXmlList"));
        Assert.Empty(item.Element("MatchingXmlList")!.Elements("MatchingXml"));
        Assert.Equal(name, item.Element("Name")?.Value);
        Assert.Equal(key, item.Element("UniversalParam")?.Value);
        Assert.Equal(specialParameter, item.Element("SpecialParamNum")?.Value);
        Assert.Equal(dataType, item.Element("DataTypeNum")?.Value);
        Assert.Equal("0", item.Element("ExecutionUPStatusNum")?.Value);
    }

    private sealed class DispatcherTemplateDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private DispatcherTemplateDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<DispatcherTemplateDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new DispatcherTemplateDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(UniEmuDbContext db)
        {
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Stopped),
                ProtocolId = 42,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.EmulatorTags.AddRange(
                new EmulatorTagEntity
                {
                    Id = "tg-power",
                    EmulatorId = "em-1",
                    Name = "Power",
                    Key = "PowerOn",
                    Type = UniEmuJson.EnumString(TagType.Bool),
                    Source = UniEmuJson.EnumString(TagSource.Static),
                    Preview = "true",
                },
                new EmulatorTagEntity
                {
                    Id = "tg-feed",
                    EmulatorId = "em-1",
                    Name = "Feed",
                    Key = "FeedRate",
                    Type = UniEmuJson.EnumString(TagType.Double),
                    Source = UniEmuJson.EnumString(TagSource.Static),
                    Preview = "120",
                    SpecialParameter = UniEmuJson.EnumString(SpecialParameter.FeedOvr),
                },
                new EmulatorTagEntity
                {
                    Id = "tg-program",
                    EmulatorId = "em-1",
                    Name = "Program",
                    Key = "ProgramName",
                    Type = UniEmuJson.EnumString(TagType.String),
                    Source = UniEmuJson.EnumString(TagSource.Static),
                    Preview = "MAIN.nc",
                    SpecialParameter = UniEmuJson.EnumString(SpecialParameter.PrgName),
                });

            await db.SaveChangesAsync();
        }
    }
}
