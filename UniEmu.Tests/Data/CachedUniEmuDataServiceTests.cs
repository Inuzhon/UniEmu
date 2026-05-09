using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;

namespace UniEmu.Tests.Data;

public sealed class CachedUniEmuDataServiceTests
{
    [Fact]
    public async Task GetEmulatorWithTagsAsync_ReturnsCachedSnapshot_OnRepeatedRead()
    {
        await using var fixture = await CacheDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedUniEmuDataService(db, cache);

        fixture.CommandCounter.Reset();

        var first = await service.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);
        var commandsAfterFirstRead = fixture.CommandCounter.CommandCount;
        var second = await service.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Main emulator", first.Name);
        Assert.Equal("Temperature", first.Tags.Single().Name);
        Assert.Equal(commandsAfterFirstRead, fixture.CommandCounter.CommandCount);
    }

    [Fact]
    public async Task InvalidateEmulator_RemovesCachedSnapshot()
    {
        await using var fixture = await CacheDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedUniEmuDataService(db, cache);

        var first = await service.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Emulators SET Name = 'Fresh emulator' WHERE Id = 'em-1'");

        service.InvalidateEmulator("em-1");
        var fresh = await service.GetEmulatorWithTagsAsync("em-1", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(fresh);
        Assert.Equal("Main emulator", first.Name);
        Assert.Equal("Fresh emulator", fresh.Name);
    }

    [Fact]
    public async Task GetVisibleScriptsAsync_ReturnsCachedSharedAndEmulatorScripts()
    {
        await using var fixture = await CacheDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedUniEmuDataService(db, cache);

        fixture.CommandCounter.Reset();

        var first = await service.GetVisibleScriptsAsync("em-1", CancellationToken.None);
        var commandsAfterFirstRead = fixture.CommandCounter.CommandCount;
        var second = await service.GetVisibleScriptsAsync("em-1", CancellationToken.None);

        Assert.Equal(["common.csx", "machine.csx"], first.Select(s => s.Name));
        Assert.Equal(["common.csx", "machine.csx"], second.Select(s => s.Name));
        Assert.Equal(commandsAfterFirstRead, fixture.CommandCounter.CommandCount);
    }

    private sealed class CacheDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private CacheDbFixture(
            SqliteConnection connection,
            DbContextOptions<UniEmuDbContext> options,
            CountingCommandInterceptor commandCounter)
        {
            this.connection = connection;
            this.options = options;
            CommandCounter = commandCounter;
        }

        public CountingCommandInterceptor CommandCounter { get; }

        public static async Task<CacheDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var commandCounter = new CountingCommandInterceptor();
            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commandCounter)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new CacheDbFixture(connection, options, commandCounter);
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
                Tags =
                [
                    new EmulatorTagEntity
                    {
                        Id = "tg-1",
                        EmulatorId = "em-1",
                        Name = "Temperature",
                        Key = "temperature",
                        Type = "double",
                        Source = "static",
                        Preview = "42",
                        TriggerJson = "{}",
                    },
                ],
            });
            db.ScriptFiles.AddRange(
                new ScriptFileEntity
                {
                    Id = "scr-1",
                    Name = "common.csx",
                    Scope = "shared",
                    Content = "int Add(int a, int b) => a + b;",
                    SizeBytes = 31,
                    UpdatedAt = now,
                },
                new ScriptFileEntity
                {
                    Id = "scr-2",
                    Name = "machine.csx",
                    Scope = "emulator",
                    EmulatorId = "em-1",
                    Content = "return 1;",
                    SizeBytes = 9,
                    UpdatedAt = now,
                });
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = "cnc-1",
                Name = "main.nc",
                Scope = "shared",
                Content = "M30",
                SizeBytes = 3,
                UpdatedAt = now,
                UploadedAt = now,
            });
            await db.SaveChangesAsync();
        }
    }

    public sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        public int CommandCount { get; private set; }

        public void Reset() => CommandCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CommandCount++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CommandCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
