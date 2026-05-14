using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TagPreviewFlushServiceTests
{
    [Fact]
    public async Task FlushAsync_WritesDirtyPreviewToDatabase()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-1", "42");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var preview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("42", preview);
    }

    [Fact]
    public async Task FlushAsync_CoalescesMultipleDirtyValues()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-1", "1");
        service.MarkDirty("em-1", "tg-1", "2");
        service.MarkDirty("em-1", "tg-1", "3");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var preview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("3", preview);
    }

    [Fact]
    public async Task FlushAsync_SkipsDeletedTags()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);

        service.MarkDirty("em-1", "tg-missing", "99");

        await service.FlushAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var existingPreview = await db.EmulatorTags.Where(t => t.Id == "tg-1").Select(t => t.Preview).SingleAsync();
        Assert.Equal("0", existingPreview);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private Fixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<UniEmuDbContext>().UseSqlite(connection).Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = "running",
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            db.EmulatorTags.Add(new EmulatorTagEntity
            {
                Id = "tg-1",
                EmulatorId = "em-1",
                Name = "Temperature",
                Key = "temperature",
                Type = "double",
                Source = "generator",
                Preview = "0",
                TriggerJson = "{}",
            });
            await db.SaveChangesAsync();

            return new Fixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
