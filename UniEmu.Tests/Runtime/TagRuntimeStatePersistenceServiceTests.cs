using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TagRuntimeStatePersistenceServiceTests
{
    [Fact]
    public async Task HydrateFromTagPreviewsAsync_LoadsStaticEventAndCronTags()
    {
        await using var fixture = await RuntimeStatePersistenceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var service = new TagRuntimeStatePersistenceService(db, stateStore);

        await service.HydrateFromTagPreviewsAsync(CancellationToken.None);

        Assert.True(stateStore.TryGet("em-1", "tag-static", out var staticValue));
        Assert.Equal(12.5, staticValue.Value);
        Assert.Equal(12.5, staticValue.NumericValue);

        Assert.True(stateStore.TryGet("em-1", "tag-start", out var startValue));
        Assert.Equal("2026-05-10T10:00:00.0000000+00:00", startValue.Value);
        Assert.Null(startValue.NumericValue);

        Assert.True(stateStore.TryGet("em-1", "tag-cron", out var cronValue));
        Assert.Equal(7, cronValue.Value);
        Assert.Equal(7, cronValue.NumericValue);

        Assert.False(stateStore.TryGet("em-1", "tag-interval", out _));
    }

    [Fact]
    public async Task PersistToTagPreviewsAsync_SavesRuntimeSnapshotToTagPreviews()
    {
        await using var fixture = await RuntimeStatePersistenceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var service = new TagRuntimeStatePersistenceService(db, stateStore);
        stateStore.Set("em-1", "tag-static", "Static tag", 25.75, 25.75, DateTimeOffset.UtcNow);
        stateStore.Set("em-1", "tag-start", "Start tag", "stopped-at-end", null, DateTimeOffset.UtcNow);
        stateStore.Set("em-unknown", "tag-missing", "Missing tag", 99, 99, DateTimeOffset.UtcNow);

        await service.PersistToTagPreviewsAsync(CancellationToken.None);

        var staticTag = await db.EmulatorTags.SingleAsync(tag => tag.Id == "tag-static");
        Assert.Equal("25.75", staticTag.Preview);

        var startTag = await db.EmulatorTags.SingleAsync(tag => tag.Id == "tag-start");
        Assert.Equal("stopped-at-end", startTag.Preview);
    }

    private sealed class RuntimeStatePersistenceDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private RuntimeStatePersistenceDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<RuntimeStatePersistenceDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new RuntimeStatePersistenceDbFixture(connection, options);
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
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.EmulatorTags.AddRange(
                CreateTag("tag-static", "Static tag", TagType.Double, TagSource.Static, "12.5", new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                CreateTag("tag-start", "Start tag", TagType.String, TagSource.Script, "2026-05-10T10:00:00.0000000+00:00", new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                CreateTag("tag-cron", "Cron tag", TagType.Int, TagSource.Script, "7", new TagTriggerDto(TagTriggerMode.Cron, null, "0 0 * * *", null, null)),
                CreateTag("tag-interval", "Interval tag", TagType.Double, TagSource.Script, "99", new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)));

            await db.SaveChangesAsync();
        }

        private static EmulatorTagEntity CreateTag(
            string id,
            string name,
            TagType type,
            TagSource source,
            string preview,
            TagTriggerDto trigger)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(type),
                Source = UniEmuJson.EnumString(source),
                Preview = preview,
                TriggerJson = UniEmuJson.Serialize(trigger),
            };
        }
    }
}
