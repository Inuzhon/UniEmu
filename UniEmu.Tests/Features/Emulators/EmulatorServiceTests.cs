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

    private static EmulatorService CreateService(UniEmuDbContext db)
    {
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var stateStore = new TagRuntimeStateStore();
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

        var scheduleService = new EmulatorScheduleService(
            db,
            dataCache,
            schedulerFactory.Object,
            stateStore,
            NullLogger<EmulatorScheduleService>.Instance,
            new ConfigurationBuilder().Build(),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));

        return new EmulatorService(
            db,
            dataCache,
            scheduleService,
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
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
}
