using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Hosting;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class EmulatorScheduleServiceTests
{
    [Fact]
    public async Task ScheduleRunningEmulatorsAsync_SchedulesExistingRunningEmulators()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleRunningEmulatorsAsync(CancellationToken.None);

        var scheduledJobs = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => (IJobDetail)invocation.Arguments[0])
            .ToList();
        Assert.Contains(scheduledJobs, job => job.Key.Equals(RuntimeJobKeys.PublishJob("em-1")));
        Assert.Contains(scheduledJobs, job => job.Key.Equals(RuntimeJobKeys.DispatcherBlockCheckJob("em-1")));
    }

    [Fact]
    public async Task ScheduleEmulatorAsync_DoesNotScheduleEventTags()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleEmulatorAsync("em-1", CancellationToken.None);

        var scheduledTagJobs = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => (IJobDetail)invocation.Arguments[0])
            .Where(job => job.JobType == typeof(TagValueJob))
            .ToList();

        var tagJob = Assert.Single(scheduledTagJobs);
        Assert.Equal("tg-interval", tagJob.JobDataMap.GetString(RuntimeJobKeys.TagId));

        var blockCheckTrigger = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => new
            {
                Job = (IJobDetail)invocation.Arguments[0],
                Trigger = (ITrigger)invocation.Arguments[1],
            })
            .Single(item => item.Job.JobType == typeof(DispatcherBlockCheckJob))
            .Trigger;

        var simpleTrigger = Assert.IsAssignableFrom<ISimpleTrigger>(blockCheckTrigger);
        Assert.Equal(TimeSpan.FromSeconds(5), simpleTrigger.RepeatInterval);
    }

    [Fact]
    public async Task ScheduleEmulatorAsync_DoesNotScheduleCalculatedProgramFrameTags()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.AddRange(
            ScheduleDbFixture.CreateProgramFrameTag("tg-frame-num", "Frame number", TagType.Int, SpecialParameter.FrameNum),
            ScheduleDbFixture.CreateProgramFrameTag("tg-frame-text", "Frame text", TagType.String, SpecialParameter.FrameText));
        await db.SaveChangesAsync();

        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleEmulatorAsync("em-1", CancellationToken.None);

        var scheduledTagIds = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => (IJobDetail)invocation.Arguments[0])
            .Where(job => job.JobType == typeof(TagValueJob))
            .Select(job => job.JobDataMap.GetString(RuntimeJobKeys.TagId))
            .ToList();

        Assert.DoesNotContain("tg-frame-num", scheduledTagIds);
        Assert.DoesNotContain("tg-frame-text", scheduledTagIds);
        Assert.Contains("tg-interval", scheduledTagIds);
    }

    [Fact]
    public async Task ScheduleEmulatorAsync_SchedulesCronTagsWithNormalizedUnixCron()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(ScheduleDbFixture.CreateCronTag("tg-cron", "Cron script", "0 0 * * *"));
        await db.SaveChangesAsync();

        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleEmulatorAsync("em-1", CancellationToken.None);

        var scheduledCron = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => new
            {
                Job = (IJobDetail)invocation.Arguments[0],
                Trigger = (ITrigger)invocation.Arguments[1],
            })
            .Single(item => item.Job.JobType == typeof(TagValueJob)
                            && item.Job.JobDataMap.GetString(RuntimeJobKeys.TagId) == "tg-cron");

        var cronTrigger = Assert.IsAssignableFrom<ICronTrigger>(scheduledCron.Trigger);
        Assert.Equal("0 0 0 * * ?", cronTrigger.CronExpressionString);
        Assert.Equal(RuntimeJobKeys.TagTrigger("em-1", "tg-cron"), cronTrigger.Key);
    }

    [Fact]
    public async Task ScheduleEmulatorAsync_NormalizesLegacyScenarioOnceTrigger_ToIntervalJob()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(ScheduleDbFixture.CreateScenarioOnceTag("tg-scenario", "Scenario"));
        await db.SaveChangesAsync();

        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleEmulatorAsync("em-1", CancellationToken.None);

        db.ChangeTracker.Clear();
        var storedTrigger = await db.EmulatorTags
            .Where(tag => tag.Id == "tg-scenario")
            .Select(tag => UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson))
            .SingleAsync();
        Assert.NotNull(storedTrigger);
        Assert.Equal(TagTriggerMode.Interval, storedTrigger.Mode);
        Assert.Equal(1, storedTrigger.IntervalValue);
        Assert.Equal(TagIntervalUnit.Sec, storedTrigger.IntervalUnit);
        Assert.Null(storedTrigger.Event);

        var scheduledScenarioJob = scheduler.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IScheduler.ScheduleJob))
            .Select(invocation => (IJobDetail)invocation.Arguments[0])
            .Single(job => job.JobType == typeof(TagValueJob)
                           && job.JobDataMap.GetString(RuntimeJobKeys.TagId) == "tg-scenario");
        Assert.Equal("tg-scenario", scheduledScenarioJob.JobDataMap.GetString(RuntimeJobKeys.TagId));
    }

    [Fact]
    public async Task ExecuteEventTagsAsync_EvaluatesOnStopScriptTag()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            Mock.Of<ISchedulerFactory>(),
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ExecuteEventTagsAsync("em-1", TagTriggerEvent.OnStop, CancellationToken.None);

        Assert.True(stateStore.TryGet("em-1", "tg-stop", out var value));
        Assert.Equal(5d, value.Value);
        Assert.Equal(5d, value.NumericValue);

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-stop");
        Assert.Equal("5", tag.Preview);
    }

    [Fact]
    public async Task ExecuteEventTagsAsync_EvaluatesOnStartScriptTag()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            Mock.Of<ISchedulerFactory>(),
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ExecuteEventTagsAsync("em-1", TagTriggerEvent.OnStart, CancellationToken.None);

        Assert.True(stateStore.TryGet("em-1", "tg-start", out var value));
        Assert.Equal(1d, value.Value);
        Assert.Equal(1d, value.NumericValue);

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-start");
        Assert.Equal("1", tag.Preview);
    }

    [Fact]
    public async Task ExecuteEventTagsAsync_StoresCalculationErrorOnEmulator()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(new EmulatorTagEntity
        {
            Id = "tg-event-throws",
            EmulatorId = "em-1",
            Name = "Throwing event tag",
            Key = "tg-event-throws",
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Script),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
            FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, "throw new InvalidOperationException(\"event boom\");")),
        });
        await db.SaveChangesAsync();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            Mock.Of<ISchedulerFactory>(),
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ExecuteEventTagsAsync("em-1", TagTriggerEvent.OnStart, CancellationToken.None);

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "em-1");
        Assert.Contains("event boom", emulator.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScheduleEmulatorAsync_StoresInvalidTagScheduleOnEmulator()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(ScheduleDbFixture.CreateCronTag("tg-bad-cron", "Bad cron", "bad cron"));
        await db.SaveChangesAsync();
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());
        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));

        await service.ScheduleEmulatorAsync("em-1", CancellationToken.None);

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "em-1");
        Assert.Contains("Некорректное расписание тега Bad cron", emulator.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnscheduleEmulatorAsync_FlushesDirtyTagPreviews()
    {
        await using var fixture = await ScheduleDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new CapturingRuntimeUpdateBroadcaster()));
        flushService.MarkDirty("em-1", "tg-interval", "123");

        await service.UnscheduleEmulatorAsync("em-1", CancellationToken.None);

        db.ChangeTracker.Clear();
        var preview = await db.EmulatorTags
            .Where(t => t.Id == "tg-interval")
            .Select(t => t.Preview)
            .SingleAsync();
        Assert.Equal("123", preview);
    }

    private sealed class ScheduleDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private ScheduleDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<ScheduleDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new ScheduleDbFixture(connection, options);
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
                CreateTag("tg-start", "Start script", TagTriggerEvent.OnStart, "return 1;"),
                CreateTag("tg-stop", "Stop script", TagTriggerEvent.OnStop, "return 5;"),
                CreateIntervalTag("tg-interval", "Interval script", "return 9;"));

            await db.SaveChangesAsync();
        }

        private static EmulatorTagEntity CreateTag(string id, string name, TagTriggerEvent ev, string script)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Script),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, ev, null, null, null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            };
        }

        private static EmulatorTagEntity CreateIntervalTag(string id, string name, string script)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Script),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            };
        }

        public static EmulatorTagEntity CreateCronTag(string id, string name, string cron)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Script),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Cron, null, cron, null, null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, "return 11;")),
            };
        }

        public static EmulatorTagEntity CreateScenarioOnceTag(string id, string name)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Scenario),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
                    [
                        new TagScenarioSegmentDto(
                            "line-up",
                            10,
                            new TagCalcConfigDto(CalcType.Line, "0", "100", 10, null, null, null, null),
                            "Line up"),
                    ],
                    ContinueOnFormulaEnd.Repeat,
                    StartValue: null)),
            };
        }

        public static EmulatorTagEntity CreateProgramFrameTag(
            string id,
            string name,
            TagType type,
            SpecialParameter specialParameter)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = id,
                Type = UniEmuJson.EnumString(type),
                Source = UniEmuJson.EnumString(TagSource.Static),
                Preview = type == TagType.Int ? "0" : string.Empty,
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
                SpecialParameter = UniEmuJson.EnumString(specialParameter),
            };
        }
    }

    private sealed class CapturingRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
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
