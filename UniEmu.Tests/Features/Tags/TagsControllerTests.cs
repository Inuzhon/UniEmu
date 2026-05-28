using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Tags;
using UniEmu.Hosting;
using UniEmu.Realtime;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Features.Tags;

public sealed class TagsControllerTests
{
    [Fact]
    public async Task List_ReturnsOkWithOrderedTags_WhenEmulatorExists()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateTag("tg-a", "Alpha", "alpha"));
        await db.SaveChangesAsync();
        var controller = new TagsController(fixture.CreateService(db));

        var result = await controller.List("em-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var tags = Assert.IsAssignableFrom<IReadOnlyList<EmulatorTagDto>>(ok.Value);
        Assert.Equal(["Alpha", "Existing"], tags.Select(tag => tag.Name));
    }

    [Fact]
    public async Task List_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TagsController(fixture.CreateService(db));

        var result = await controller.List("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameOrKeyIsMissing()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TagsController(fixture.CreateService(db));

        var result = await controller.Create(
            "em-1",
            CreateRequest(" ", "key"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Имя тега обязательно.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TagsController(fixture.CreateService(db));

        var result = await controller.Create(
            "em-1",
            CreateRequest("New tag", "new_tag"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TagsController.List), created.ActionName);
        var tag = Assert.IsType<EmulatorTagDto>(created.Value);
        Assert.Equal("New tag", tag.Name);
        Assert.Equal("new_tag", tag.Key);
    }

    [Fact]
    public async Task Replace_ReturnsBadRequest_WhenTagNameAlreadyExists()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateTag("tg-other", "Other", "other"));
        await db.SaveChangesAsync();
        var controller = new TagsController(fixture.CreateService(db));

        var result = await controller.Replace(
            "em-1",
            "tg-existing",
            new ReplaceTagRequest(
                " other ",
                "existing",
                TagType.Double,
                TagSource.Formula,
                "(computed)",
                new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
                null,
                new TagFormulaConfigDto(null, "return 1;"),
                null,
                true,
                null,
                null,
                null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Тег с таким именем уже существует в этом эмуляторе.", badRequest.Value);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentThenNotFound()
    {
        await using var fixture = await TagsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TagsController(fixture.CreateService(db));

        var deleted = await controller.Delete("em-1", "tg-existing", CancellationToken.None);
        var deletedAgain = await controller.Delete("em-1", "tg-existing", CancellationToken.None);

        Assert.IsType<NoContentResult>(deleted);
        Assert.IsType<NotFoundResult>(deletedAgain);
    }

    private static CreateTagRequest CreateRequest(string name, string key) => new(
        name,
        key,
        TagType.Double,
        TagSource.Formula,
        "(computed)",
        new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
        null,
        new TagFormulaConfigDto(null, "return 1;"),
        null,
        true,
        null,
        null,
        null);

    private static EmulatorTagEntity CreateTag(string id, string name, string key) => new()
    {
        Id = id,
        EmulatorId = "em-1",
        Name = name,
        Key = key,
        Type = UniEmuJson.EnumString(TagType.Double),
        Source = UniEmuJson.EnumString(TagSource.Static),
        Preview = "0",
        TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
        Enabled = true,
    };

    private sealed class TagsControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private TagsControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<TagsControllerDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Stopped),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            db.EmulatorTags.Add(CreateTag("tg-existing", "Existing", "existing"));
            await db.SaveChangesAsync();

            return new TagsControllerDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public TagService CreateService(UniEmuDbContext db)
        {
            var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
            var stateStore = new TagRuntimeStateStore();
            var scheduleService = new EmulatorScheduleService(
                db,
                dataCache,
                Mock.Of<ISchedulerFactory>(),
                stateStore,
                new TagPreviewFlushService(() => db, NullLogger<TagPreviewFlushService>.Instance),
                NullLogger<EmulatorScheduleService>.Instance,
                Options.Create(new UniEmuOptions()),
                new TelemetryValueGenerator(),
                new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
                new RuntimeUpdateService(Mock.Of<IRuntimeUpdateBroadcaster>()));

            return new TagService(db, dataCache, scheduleService, new CsxLanguageService());
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
