using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Emulators;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Tests.Features.Emulators;

public sealed class EmulatorServiceTests
{
    [Fact]
    public async Task DeleteAsync_RemovesEmulatorAndOwnedRows()
    {
        await using var fixture = await EmulatorServiceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        stateStore.Set("em-1", "tg-start", "Start script", 1d, 1d, DateTimeOffset.UtcNow);
        var service = CreateService(db, stateStore);

        var deleted = await service.DeleteAsync("em-1", CancellationToken.None);
        var deletedAgain = await service.DeleteAsync("em-1", CancellationToken.None);

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Empty(await db.Emulators.ToListAsync());
        Assert.Empty(await db.EmulatorTags.ToListAsync());
        Assert.Empty(await db.ScriptFiles.ToListAsync());
        Assert.Empty(await db.CncPrograms.ToListAsync());
        Assert.Empty(await db.TelemetryPoints.ToListAsync());
        Assert.Empty(await db.SystemEvents.ToListAsync());
        Assert.Empty(await db.ScriptRuntimeStates.ToListAsync());
        Assert.False(stateStore.TryGet("em-1", "tg-start", out _));
    }

    [Fact]
    public async Task PatchStatusAsync_DoesNotEvaluateOnStartTags_WhenEmulatorIsAlreadyRunning()
    {
        await using var fixture = await EmulatorServiceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        await service.PatchStatusAsync(
            "em-1",
            new PatchEmulatorStatusRequest(EmulatorStatus.Running),
            CancellationToken.None);

        var tag = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-start");
        Assert.Equal("0", tag.Preview);
    }

    [Fact]
    public async Task PatchStatusAsync_CreatesSuccessEvent_WhenEmulatorStarts()
    {
        await using var fixture = await EmulatorServiceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.Emulators.Add(new EmulatorEntity
        {
            Id = "em-2",
            Name = "Secondary emulator",
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = 18,
            TargetUrl = "http://localhost",
            IntervalSec = 1,
        });
        await db.SaveChangesAsync();

        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = CreateService(db, broadcaster: broadcaster);

        await service.PatchStatusAsync(
            "em-2",
            new PatchEmulatorStatusRequest(EmulatorStatus.Running),
            CancellationToken.None);

        var ev = await db.SystemEvents.SingleAsync(e => e.EmulatorId == "em-2");
        Assert.Equal("Secondary emulator", ev.EmulatorName);
        Assert.Equal(UniEmuJson.EnumString(EventLevel.Success), ev.Level);
        Assert.Equal("Эмулятор запущен", ev.Message);
        Assert.Contains(broadcaster.EventUpdates, update => update.Event.Id == ev.Id);
    }

    [Fact]
    public async Task PatchStatusAsync_CreatesInfoEvent_WhenEmulatorStops()
    {
        await using var fixture = await EmulatorServiceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = CreateService(db, broadcaster: broadcaster);

        await service.PatchStatusAsync(
            "em-1",
            new PatchEmulatorStatusRequest(EmulatorStatus.Stopped),
            CancellationToken.None);

        var ev = await db.SystemEvents.SingleAsync(e => e.EmulatorId == "em-1" && e.Message == "Эмулятор остановлен");
        Assert.Equal("Main emulator", ev.EmulatorName);
        Assert.Equal(UniEmuJson.EnumString(EventLevel.Info), ev.Level);
        Assert.Contains(broadcaster.EventUpdates, update => update.Event.Id == ev.Id);
    }

    [Fact]
    public async Task PatchStatusAsync_DoesNotCreateEvent_WhenStatusDoesNotChange()
    {
        await using var fixture = await EmulatorServiceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        await service.PatchStatusAsync(
            "em-1",
            new PatchEmulatorStatusRequest(EmulatorStatus.Running),
            CancellationToken.None);

        Assert.DoesNotContain(
            await db.SystemEvents.ToListAsync(),
            ev => ev.Message is "Эмулятор запущен" or "Эмулятор остановлен");
    }

    private static EmulatorService CreateService(
        UniEmuDbContext db,
        TagRuntimeStateStore? stateStore = null,
        IRuntimeUpdateBroadcaster? broadcaster = null)
    {
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        stateStore ??= new TagRuntimeStateStore();
        broadcaster ??= new NoopRuntimeUpdateBroadcaster();
        var runtimeUpdateService = new RuntimeUpdateService(broadcaster);
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        scheduler
            .Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey>());
        scheduler
            .Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        scheduler
            .Setup(s => s.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scheduler.Object);

        var scheduleService = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            NullLogger<EmulatorScheduleService>.Instance,
            new ConfigurationBuilder().Build(),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            runtimeUpdateService);

        return new EmulatorService(
            db,
            dataCache,
            scheduleService,
            runtimeUpdateService);
    }

    private sealed class EmulatorServiceDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private EmulatorServiceDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<EmulatorServiceDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new EmulatorServiceDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(UniEmuDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            db.EmulatorTags.Add(new EmulatorTagEntity
            {
                Id = "tg-start",
                EmulatorId = "em-1",
                Name = "Start script",
                Key = "start",
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Script),
                Preview = "0",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, "return 11;")),
            });
            db.ScriptFiles.Add(new ScriptFileEntity
            {
                Id = "scr-1",
                EmulatorId = "em-1",
                Name = "machine.csx",
                Scope = "emulator",
                Content = "return 1;",
                UpdatedAt = now,
                SizeBytes = 9,
            });
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = "cnc-1",
                EmulatorId = "em-1",
                Name = "machine.nc",
                Scope = "emulator",
                Content = "M30",
                Description = "Owned program",
                UpdatedAt = now,
                UploadedAt = now,
                SizeBytes = 3,
            });
            db.TelemetryPoints.Add(new TelemetryPointEntity
            {
                EmulatorId = "em-1",
                Timestamp = now,
                ValuesJson = "{}",
            });
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = "ev-1",
                EmulatorId = "em-1",
                EmulatorName = "Main emulator",
                Level = "info",
                Message = "Created",
                Timestamp = now,
            });
            db.ScriptRuntimeStates.Add(new ScriptRuntimeStateEntity
            {
                Id = "state-1",
                EmulatorId = "em-1",
                ScriptKey = "machine.csx",
                ValuesJson = "{}",
                UpdatedAt = now,
            });

            await db.SaveChangesAsync();
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

    private sealed class RecordingRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public List<(SystemEventDto Event, IReadOnlyList<string> Groups)> EventUpdates { get; } = [];

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
            EventUpdates.Add((ev, groups));
            return Task.CompletedTask;
        }
    }
}
