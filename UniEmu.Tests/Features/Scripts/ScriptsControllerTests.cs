using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Common;
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
