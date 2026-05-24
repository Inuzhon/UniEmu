using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Common;
using UniEmu.Features.Scripts;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Features.Scripts;

public sealed class ScriptsControllerTests
{
    [Fact]
    public async Task List_ReturnsOkWithScripts()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var result = await controller.List(ScriptScope.Shared, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var scripts = Assert.IsAssignableFrom<IReadOnlyList<ScriptFileDto>>(ok.Value);
        var script = Assert.Single(scripts);
        Assert.Equal("common.csx", script.Name);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsMissing()
    {
        var controller = new ScriptsController(null!);

        var result = await controller.Create(
            new CreateScriptRequest(" ", ScriptScope.Shared, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Name is required.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenScriptNameAlreadyExists()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var result = await controller.Create(
            new CreateScriptRequest("common", ScriptScope.Shared, null),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("Скрипт с таким именем уже существует в этой области видимости.", conflict.Value);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenScopeCombinationIsInvalid()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var result = await controller.Create(
            new CreateScriptRequest("local", ScriptScope.Emulator, "missing"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid scope/emulatorId combination.", badRequest.Value);
    }

    [Fact]
    public async Task Patch_ReturnsOk_WhenScriptExists()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var result = await controller.Patch(
            "scr-shared",
            new PatchScriptRequest("renamed", "return 2;"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var script = Assert.IsType<ScriptFileDto>(ok.Value);
        Assert.Equal("renamed.csx", script.Name);
        Assert.Equal("return 2;", script.Content);
    }

    [Fact]
    public async Task Patch_ReturnsNotFound_WhenScriptDoesNotExist()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var result = await controller.Patch(
            "missing",
            new PatchScriptRequest("missing", null),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentThenNotFound()
    {
        await using var fixture = await ScriptsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new ScriptsController(CreateService(db));

        var deleted = await controller.Delete("scr-shared", CancellationToken.None);
        var deletedAgain = await controller.Delete("scr-shared", CancellationToken.None);

        Assert.IsType<NoContentResult>(deleted);
        Assert.IsType<NotFoundResult>(deletedAgain);
    }

    private static ScriptService CreateService(UniEmuDbContext db)
    {
        return new ScriptService(
            db,
            new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions())),
            new ScopedResourceValidator(db),
            new CsxLanguageService(),
            new CompiledTagScriptCache());
    }

    private sealed class ScriptsControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private ScriptsControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<ScriptsControllerDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.ScriptFiles.Add(new ScriptFileEntity
            {
                Id = "scr-shared",
                Name = "common.csx",
                Scope = UniEmuJson.EnumString(ScriptScope.Shared),
                Content = "return 1;",
                SizeBytes = 9,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            return new ScriptsControllerDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
