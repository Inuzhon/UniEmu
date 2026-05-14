using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Emulators;

namespace UniEmu.Tests.Features.Emulators;

public sealed class EmulatorsControllerTests
{
    [Fact]
    public async Task GetDispatcherTemplate_ReturnsXmlFile_WhenEmulatorExists()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var templateService = new DispatcherTemplateService(db);
        var controller = new EmulatorsController(null!, templateService);

        var result = await controller.GetDispatcherTemplate("em-1", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/xml; charset=utf-8", file.ContentType);
        Assert.Equal("Universal_template_machineID_7.xml", file.FileDownloadName);

        var content = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("<Name>PowerOn</Name>", content);
    }

    [Fact]
    public async Task GetDispatcherTemplate_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var templateService = new DispatcherTemplateService(db);
        var controller = new EmulatorsController(null!, templateService);

        var result = await controller.GetDispatcherTemplate("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class EmulatorsControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private EmulatorsControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<EmulatorsControllerDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new EmulatorsControllerDbFixture(connection, options);
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
                ProtocolId = 7,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.EmulatorTags.Add(new EmulatorTagEntity
            {
                Id = "tg-power",
                EmulatorId = "em-1",
                Name = "Power",
                Key = "PowerOn",
                Type = UniEmuJson.EnumString(TagType.Bool),
                Source = UniEmuJson.EnumString(TagSource.Static),
                Preview = "true",
            });

            await db.SaveChangesAsync();
        }
    }
}
