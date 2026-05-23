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
using UniEmu.Features.CncPrograms;
using UniEmu.Features.Common;

namespace UniEmu.Tests.Features.CncPrograms;

public sealed class CncProgramsControllerTests
{
    [Fact]
    public async Task List_ReturnsOkWithPrograms()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var result = await controller.List(CncScope.Shared, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var programs = Assert.IsAssignableFrom<IReadOnlyList<CncProgramDto>>(ok.Value);
        Assert.Contains(programs, program => program.Name == "shared.nc");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsMissing()
    {
        var controller = new CncProgramsController(null!);

        var result = await controller.Create(
            new CreateCncProgramRequest(" ", CncScope.Shared, null, "M02", 0, null, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Name is required.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenProgramNameAlreadyExists()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var result = await controller.Create(
            new CreateCncProgramRequest("shared.nc", CncScope.Shared, null, "M02", 0, null, null),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("CNC-программа с таким именем уже существует в этой области видимости.", conflict.Value);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenScopeCombinationIsInvalid()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var result = await controller.Create(
            new CreateCncProgramRequest("local.nc", CncScope.Emulator, "missing", "M02", 0, null, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid scope/emulatorId combination.", badRequest.Value);
    }

    [Fact]
    public async Task CreateForEmulator_ReturnsCreated_WhenEmulatorExists()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var result = await controller.CreateForEmulator(
            "em-1",
            new CreateCncProgramRequest("local.nc", CncScope.Shared, null, "G01", 0, null, null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var program = Assert.IsType<CncProgramDto>(created.Value);
        Assert.Equal(CncScope.Emulator, program.Scope);
        Assert.Equal("em-1", program.EmulatorId);
    }

    [Fact]
    public async Task Patch_ReturnsOk_WhenProgramExists()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var result = await controller.Patch(
            "cnc-shared",
            new PatchCncProgramRequest("updated.nc", "Updated", "M30"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var program = Assert.IsType<CncProgramDto>(ok.Value);
        Assert.Equal("updated.nc", program.Name);
        Assert.Equal("Updated", program.Description);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentThenNotFound()
    {
        await using var fixture = await CncProgramsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new CncProgramsController(fixture.CreateService(db));

        var deleted = await controller.Delete("cnc-shared", CancellationToken.None);
        var deletedAgain = await controller.Delete("cnc-shared", CancellationToken.None);

        Assert.IsType<NoContentResult>(deleted);
        Assert.IsType<NotFoundResult>(deletedAgain);
    }

    private sealed class CncProgramsControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private CncProgramsControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<CncProgramsControllerDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Stopped),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = "cnc-shared",
                Name = "shared.nc",
                Scope = UniEmuJson.EnumString(CncScope.Shared),
                Content = "M30",
                Description = string.Empty,
                SizeBytes = 3,
                UpdatedAt = now,
                UploadedAt = now,
            });
            await db.SaveChangesAsync();

            return new CncProgramsControllerDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public CncProgramService CreateService(UniEmuDbContext db)
        {
            return new CncProgramService(
                db,
                new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions())),
                new ScopedResourceValidator(db));
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
