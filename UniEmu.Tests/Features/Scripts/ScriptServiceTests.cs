using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Scripts;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Features.Scripts;

public sealed class ScriptServiceTests
{
    [Fact]
    public async Task PatchAsync_RejectsInvalidCsxContent_AndKeepsExistingContent()
    {
        await using var fixture = await ScriptDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<CsxScriptValidationException>(() =>
            service.PatchAsync("scr-machine", new PatchScriptRequest(null, "return MissingValue;"), CancellationToken.None));

        Assert.Contains(exception.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CS0103");

        var stored = await db.ScriptFiles.SingleAsync(script => script.Id == "scr-machine");
        Assert.Equal("return 1;", stored.Content);
    }

    [Fact]
    public async Task PatchAsync_AllowsValidCsxContent_WithLoadDirective()
    {
        await using var fixture = await ScriptDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var script = await service.PatchAsync(
            "scr-machine",
            new PatchScriptRequest(null, "#load \"common.csx\"\nreturn Add(1, 2);"),
            CancellationToken.None);

        Assert.NotNull(script);
        Assert.Equal("#load \"common.csx\"\nreturn Add(1, 2);", script.Content);
    }

    private static ScriptService CreateService(UniEmuDbContext db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new ScriptService(db, new CachedUniEmuDataService(db, cache), new CsxLanguageService());
    }

    private sealed class ScriptDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private ScriptDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<ScriptDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new ScriptDbFixture(connection, options);
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

            db.ScriptFiles.AddRange(
                new ScriptFileEntity
                {
                    Id = "scr-shared",
                    Name = "common.csx",
                    Scope = "shared",
                    Content = "int Add(int a, int b) => a + b;",
                    SizeBytes = 31,
                    UpdatedAt = now,
                },
                new ScriptFileEntity
                {
                    Id = "scr-machine",
                    Name = "machine.csx",
                    Scope = "emulator",
                    EmulatorId = "em-1",
                    Content = "return 1;",
                    SizeBytes = 9,
                    UpdatedAt = now,
                });

            await db.SaveChangesAsync();
        }
    }
}
