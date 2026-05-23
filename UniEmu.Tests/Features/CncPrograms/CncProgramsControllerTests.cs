using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Common;
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
