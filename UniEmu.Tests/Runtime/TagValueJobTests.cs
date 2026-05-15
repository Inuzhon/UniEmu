using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using System.Globalization;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Hosting;
using UniEmu.Realtime;
using UniEmu.Runtime;
using UniEmu.Tests.Hosting;

namespace UniEmu.Tests.Runtime;

[Collection(ApplicationGlobalizationCollection.Name)]
public sealed class TagValueJobTests
{
    [Fact]
    public async Task Execute_PersistsGeneratedScriptValueToTagPreview()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);
        var context = CreateContext("tg-start");

        await job.Execute(context);
        await flushService.FlushAsync();

        var events = await db.SystemEvents.Select(e => e.Message).ToListAsync();
        Assert.True(stateStore.TryGet("em-1", "tg-start", out var value), string.Join(Environment.NewLine, events));
        Assert.Equal(5d, value.Value);
        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-start");
        Assert.Equal("5", tag.Preview);
    }

    [Fact]
    public async Task Execute_PersistsNowScriptResultToStringTagPreview()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-now"));
        await flushService.FlushAsync();

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-now");
        Assert.True(DateTimeOffset.TryParse(tag.Preview, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
    }

    [Fact]
    public async Task Execute_ExposesApplicationTimeZoneToScriptNow()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-now-offset"));
        await flushService.FlushAsync();

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-now-offset");
        Assert.Equal("3", tag.Preview);
    }

    [Fact]
    public async Task Execute_PersistsCronScriptValueToTagPreview()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-cron"));
        await flushService.FlushAsync();

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-cron");
        Assert.Equal("17", tag.Preview);
        Assert.True(stateStore.TryGet("em-1", "tg-cron", out var value));
        Assert.Equal(17d, value.Value);
        Assert.Equal(17d, value.NumericValue);
    }

    [Fact]
    public async Task Execute_FormulaScriptPassesGeneratedValueToScriptTagValue()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-formula-script"));
        await flushService.FlushAsync();

        Assert.True(stateStore.TryGet("em-1", "tg-formula-script", out var value));
        Assert.Equal(42d, value.Value);
        Assert.Equal(42d, value.NumericValue);
        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-formula-script");
        Assert.Equal("42", tag.Preview);
    }

    [Fact]
    public async Task Execute_DoesNotPersistPreviewBeforeFlush()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-cron"));

        db.ChangeTracker.Clear();
        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-cron");
        Assert.Equal("(computed)", tag.Preview);
        Assert.True(stateStore.TryGet("em-1", "tg-cron", out var value));
        Assert.Equal(17d, value.Value);
    }

    [Fact]
    public async Task Execute_StoresTagCalculationErrorOnEmulator()
    {
        await using var fixture = await TagValueJobDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var job = new TagValueJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            flushService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<TagValueJob>.Instance);

        await job.Execute(CreateContext("tg-throws"));

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "em-1");
        Assert.Contains("boom", emulator.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private static IJobExecutionContext CreateContext(string tagId)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(c => c.MergedJobDataMap).Returns(new JobDataMap
        {
            [RuntimeJobKeys.EmulatorId] = "em-1",
            [RuntimeJobKeys.TagId] = tagId,
        });

        return context.Object;
    }

    private sealed class TagValueJobDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private TagValueJobDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<TagValueJobDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new TagValueJobDbFixture(connection, options);
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
                CreateTag("tg-start", "Start script", "start", TagType.Double, "return 5;"),
                CreateTag("tg-now", "Now script", "now", TagType.String, "return Now;"),
                CreateTag("tg-now-offset", "Now offset script", "now-offset", TagType.Double, "return Now.Offset.TotalHours;"),
                CreateFormulaScriptTag("tg-formula-script", "Formula script", "formula-script"),
                CreateTag(
                    "tg-cron",
                    "Cron script",
                    "cron",
                    TagType.Double,
                    "return 17;",
                    new TagTriggerDto(TagTriggerMode.Cron, null, "0 0 * * *", null, null)),
                CreateTag("tg-throws", "Throwing script", "throws", TagType.Double, "throw new InvalidOperationException(\"boom\");"));

            await db.SaveChangesAsync();
        }

        private static EmulatorTagEntity CreateTag(
            string id,
            string name,
            string key,
            TagType type,
            string script,
            TagTriggerDto? trigger = null)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = key,
                Type = UniEmuJson.EnumString(type),
                Source = UniEmuJson.EnumString(TagSource.Script),
                Preview = "(computed)",
                TriggerJson = UniEmuJson.Serialize(trigger ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            };
        }

        private static EmulatorTagEntity CreateFormulaScriptTag(string id, string name, string key)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = key,
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.FormulaScript),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
                    CalcType.Sequence,
                    Start: "[21]",
                    Finish: null,
                    Duration: 1,
                    Amplitude: null,
                    Period: null,
                    Curvature: null,
                    Distortion: null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, "return (double)UniEmu.Tag.Value! * 2;")),
            };
        }
    }

    private sealed class NoopRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
