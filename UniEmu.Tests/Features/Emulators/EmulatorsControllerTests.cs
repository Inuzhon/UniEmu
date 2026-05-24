using System.Text;
using Microsoft.AspNetCore.Mvc;
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
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Emulators;
using UniEmu.Hosting;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Tests.Features.Emulators;

public sealed class EmulatorsControllerTests
{
    [Fact]
    public async Task List_ReturnsOkWithEmulators()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new EmulatorsController(CreateService(db), new DispatcherTemplateService(db));

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var emulators = Assert.IsAssignableFrom<IReadOnlyList<EmulatorDto>>(ok.Value);
        var emulator = Assert.Single(emulators);
        Assert.Equal("em-1", emulator.Id);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new EmulatorsController(CreateService(db), new DispatcherTemplateService(db));

        var result = await controller.Get("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenProtocolIdIsNotPositive()
    {
        var controller = new EmulatorsController(null!, null!);

        var result = await controller.Create(
            new CreateEmulatorRequest("New", "http://target", 1, 0),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("protocolId must be a positive number.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new EmulatorsController(CreateService(db), new DispatcherTemplateService(db));

        var result = await controller.Create(
            new CreateEmulatorRequest("Created", "http://created", 2, 99),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(EmulatorsController.Get), created.ActionName);
        var emulator = Assert.IsType<EmulatorDto>(created.Value);
        Assert.Equal("Created", emulator.Name);
    }

    [Fact]
    public async Task Patch_ReturnsBadRequest_WhenProtocolIdIsNotPositive()
    {
        var controller = new EmulatorsController(null!, null!);

        var result = await controller.Patch(
            "em-1",
            new PatchEmulatorRequest(null, null, null, 0),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("protocolId must be a positive number.", badRequest.Value);
    }

    [Fact]
    public async Task PatchStatus_ReturnsOk_WhenEmulatorExists()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new EmulatorsController(CreateService(db), new DispatcherTemplateService(db));

        var result = await controller.PatchStatus(
            "em-1",
            new PatchEmulatorStatusRequest(EmulatorStatus.Running),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var emulator = Assert.IsType<EmulatorDto>(ok.Value);
        Assert.Equal(EmulatorStatus.Running, emulator.Status);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentThenNotFound()
    {
        await using var fixture = await EmulatorsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new EmulatorsController(CreateService(db), new DispatcherTemplateService(db));

        var deleted = await controller.Delete("em-1", CancellationToken.None);
        var deletedAgain = await controller.Delete("em-1", CancellationToken.None);

        Assert.IsType<NoContentResult>(deleted);
        Assert.IsType<NotFoundResult>(deletedAgain);
    }

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
        Assert.Contains("<Name>Power</Name>", content);
        Assert.Contains("<UniversalParam>PowerOn</UniversalParam>", content);
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

    private static EmulatorService CreateService(UniEmuDbContext db)
    {
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var stateStore = new TagRuntimeStateStore();
        var runtimeUpdateService = new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster());
        var flushService = new TagPreviewFlushService(() => db, NullLogger<TagPreviewFlushService>.Instance);
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
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            runtimeUpdateService);

        return new EmulatorService(db, dataCache, scheduleService, runtimeUpdateService);
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

    private sealed class NoopRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
