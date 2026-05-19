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

public sealed class CncProgramServiceTests
{
    [Fact]
    public async Task ListAsync_FiltersByScopeAndOrdersProgramsByName()
    {
        await using var fixture = await CncProgramDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var programs = await service.ListAsync(CncScope.Shared, null, CancellationToken.None);

        Assert.Equal(["alpha.nc", "shared.nc"], programs.Select(program => program.Name));
        Assert.All(programs, program => Assert.Equal(CncScope.Shared, program.Scope));
    }

    [Fact]
    public async Task CreateForEmulatorAsync_UsesRouteEmulatorAndRefreshesVisibleProgramCache()
    {
        await using var fixture = await CncProgramDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var beforeCreate = await fixture.DataCache.GetVisibleCncProgramsAsync("em-1", CancellationToken.None);

        var created = await service.CreateForEmulatorAsync(
            "em-1",
            new CreateCncProgramRequest("  contour.nc  ", CncScope.Shared, null, "G01 X10", 0, false, null),
            CancellationToken.None);
        var afterCreate = await fixture.DataCache.GetVisibleCncProgramsAsync("em-1", CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal("contour.nc", created.Name);
        Assert.Equal(CncScope.Emulator, created.Scope);
        Assert.Equal("em-1", created.EmulatorId);
        Assert.Equal("G01 X10".Length, created.SizeBytes);
        Assert.DoesNotContain(beforeCreate, program => program.Name == "contour.nc");
        Assert.Contains(afterCreate, program => program.Name == "contour.nc" && program.EmulatorId == "em-1");
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenEmulatorScopePointsToMissingEmulator()
    {
        await using var fixture = await CncProgramDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateAsync(
            new CreateCncProgramRequest("ghost.nc", CncScope.Emulator, "missing", "M30", 3, null, null),
            CancellationToken.None);

        Assert.Null(created);
        Assert.False(await db.CncPrograms.AnyAsync(program => program.Name == "ghost.nc"));
    }

    [Fact]
    public async Task PatchAsync_UpdatesContentSizeAndRefreshesVisibleProgramCache()
    {
        await using var fixture = await CncProgramDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var cached = await fixture.DataCache.GetVisibleCncProgramsAsync("em-1", CancellationToken.None);

        var patched = await service.PatchAsync(
            "cnc-local",
            new PatchCncProgramRequest(" local-finish.nc ", "Finish pass", "G01 Z-1"),
            CancellationToken.None);
        var fresh = await fixture.DataCache.GetVisibleCncProgramsAsync("em-1", CancellationToken.None);

        Assert.NotNull(patched);
        Assert.Equal("local-finish.nc", patched.Name);
        Assert.Equal("Finish pass", patched.Description);
        Assert.Equal("G01 Z-1".Length, patched.SizeBytes);
        Assert.Contains(cached, program => program.Name == "local.nc");
        Assert.Contains(fresh, program => program.Name == "local-finish.nc" && program.SizeBytes == "G01 Z-1".Length);
    }

    private sealed class CncProgramDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private CncProgramDbFixture(
            SqliteConnection connection,
            DbContextOptions<UniEmuDbContext> options,
            MemoryCache cache)
        {
            this.connection = connection;
            this.options = options;
            DataCache = new CachedUniEmuDataService(CreateDbContext(), cache);
        }

        public CachedUniEmuDataService DataCache { get; }

        public static async Task<CncProgramDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new CncProgramDbFixture(connection, options, new MemoryCache(new MemoryCacheOptions()));
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public CncProgramService CreateService(UniEmuDbContext db)
        {
            return new CncProgramService(db, DataCache, new ScopedResourceValidator(db));
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(UniEmuDbContext db)
        {
            var now = DateTimeOffset.Parse("2026-05-10T12:00:00Z");

            db.Emulators.AddRange(
                new EmulatorEntity
                {
                    Id = "em-1",
                    Name = "Main emulator",
                    Status = nameof(EmulatorStatus.Running),
                    ProtocolId = 18,
                    TargetUrl = "http://localhost",
                    IntervalSec = 1,
                },
                new EmulatorEntity
                {
                    Id = "em-2",
                    Name = "Other emulator",
                    Status = nameof(EmulatorStatus.Stopped),
                    ProtocolId = 19,
                    TargetUrl = "http://localhost:18080",
                    IntervalSec = 2,
                });

            db.CncPrograms.AddRange(
                new CncProgramEntity
                {
                    Id = "cnc-shared",
                    Name = "shared.nc",
                    Scope = UniEmuJson.EnumString(CncScope.Shared),
                    Content = "M30",
                    SizeBytes = 3,
                    UpdatedAt = now,
                    UploadedAt = now,
                },
                new CncProgramEntity
                {
                    Id = "cnc-alpha",
                    Name = "alpha.nc",
                    Scope = UniEmuJson.EnumString(CncScope.Shared),
                    Content = "G00",
                    SizeBytes = 3,
                    UpdatedAt = now,
                    UploadedAt = now,
                },
                new CncProgramEntity
                {
                    Id = "cnc-local",
                    Name = "local.nc",
                    Scope = UniEmuJson.EnumString(CncScope.Emulator),
                    EmulatorId = "em-1",
                    Content = "G01",
                    SizeBytes = 3,
                    UpdatedAt = now,
                    UploadedAt = now,
                },
                new CncProgramEntity
                {
                    Id = "cnc-foreign",
                    Name = "other.nc",
                    Scope = UniEmuJson.EnumString(CncScope.Emulator),
                    EmulatorId = "em-2",
                    Content = "G02",
                    SizeBytes = 3,
                    UpdatedAt = now,
                    UploadedAt = now,
                });

            await db.SaveChangesAsync();
        }
    }
}
