using System.Net;
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

public sealed class DispatcherBlockCheckJobTests
{
    [Fact]
    public async Task Execute_MarksRunningEmulatorAsError_WhenDispatcherBlocksProtocol()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Emulators.Add(new EmulatorEntity
        {
            Id = "em-1",
            Name = "Blocked",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 2,
            TargetUrl = "http://dispatcher",
            IntervalSec = 1,
            StartedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var schedulerFactory = CreateSchedulerFactory();
        var job = new DispatcherBlockCheckJob(
            db,
            dataCache,
            CreateScheduleService(options, db, dataCache, stateStore, schedulerFactory.Object),
            CreateSender("1"),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<DispatcherBlockCheckJob>.Instance);
        var context = new Mock<IJobExecutionContext>();
        var dataMap = new JobDataMap { { RuntimeJobKeys.EmulatorId, "em-1" } };
        context.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "em-1");
        Assert.Equal(nameof(EmulatorStatus.Error), emulator.Status);
        Assert.Equal("Протокол 2 заблокирован Dispatcher", emulator.LastError);
        Assert.Contains(await db.SystemEvents.ToListAsync(), ev =>
            ev.Level == UniEmuJson.EnumString(EventLevel.Error) &&
            ev.Message == "Протокол 2 заблокирован Dispatcher");
    }

    [Fact]
    public async Task Execute_ClearsRuntimeSideEffects_WhenDispatcherBlocksProtocol()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Emulators.Add(new EmulatorEntity
        {
            Id = "em-1",
            Name = "Blocked",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 2,
            TargetUrl = "http://dispatcher",
            IntervalSec = 1,
            StartedAt = DateTimeOffset.UtcNow,
        });
        db.EmulatorTags.Add(new EmulatorTagEntity
        {
            Id = "tag-1",
            EmulatorId = "em-1",
            Name = "Tag 1",
            Key = "tag-1",
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Script),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
            FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, "return 1;")),
        });
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        stateStore.Set("em-1", "tag-1", "Tag 1", 42d, 42d, DateTimeOffset.UtcNow);
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var cachedBeforeBlock = await dataCache.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);
        Assert.Equal(nameof(EmulatorStatus.Running), cachedBeforeBlock?.Status);

        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        scheduler
            .Setup(s => s.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        scheduler
            .Setup(s => s.GetJobKeys(
                It.Is<GroupMatcher<JobKey>>(matcher => matcher.CompareWithOperator == StringOperator.Equality),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey> { RuntimeJobKeys.TagJob("em-1", "tag-1") });
        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var flushService = new TagPreviewFlushService(() => new UniEmuDbContext(options), NullLogger<TagPreviewFlushService>.Instance);
        var scheduleService = CreateScheduleService(options, db, dataCache, stateStore, schedulerFactory.Object);
        var job = new DispatcherBlockCheckJob(
            db,
            dataCache,
            scheduleService,
            CreateSender("1"),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<DispatcherBlockCheckJob>.Instance);
        var context = new Mock<IJobExecutionContext>();
        var dataMap = new JobDataMap { { RuntimeJobKeys.EmulatorId, "em-1" } };
        context.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        db.ChangeTracker.Clear();
        var cachedAfterBlock = await dataCache.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);
        Assert.Equal(nameof(EmulatorStatus.Error), cachedAfterBlock?.Status);
        Assert.False(stateStore.TryGet("em-1", "tag-1", out _));
        scheduler.Verify(s => s.DeleteJob(RuntimeJobKeys.PublishJob("em-1"), It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(s => s.DeleteJob(RuntimeJobKeys.DispatcherBlockCheckJob("em-1"), It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(s => s.DeleteJobs(
            It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Contains(RuntimeJobKeys.TagJob("em-1", "tag-1"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ISchedulerFactory> CreateSchedulerFactory()
    {
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        scheduler
            .Setup(s => s.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);
        return schedulerFactory;
    }

    private static EmulatorScheduleService CreateScheduleService(
        DbContextOptions<UniEmuDbContext> options,
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        ISchedulerFactory schedulerFactory)
    {
        var flushService = new TagPreviewFlushService(() => new UniEmuDbContext(options), NullLogger<TagPreviewFlushService>.Instance);
        return new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory,
            stateStore,
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
    }

    private static TelemetryPacketSender CreateSender(string blockedAnswer)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TelemetryPacketSender)))
            .Returns(new HttpClient(new BlockedHandler(blockedAnswer)));

        return new TelemetryPacketSender(factory.Object, NullLogger<TelemetryPacketSender>.Instance);
    }

    private sealed class BlockedHandler(string blockedAnswer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(blockedAnswer),
            });
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
