using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Tags;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Features.Tags;

public sealed class TagServiceScriptValidationTests
{
    [Fact]
    public async Task CreateAsync_RejectsInlineScript_WithCompilerError()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<CsxScriptValidationException>(() =>
            service.CreateAsync("em-1", CreateRequest("return MissingValue;"), CancellationToken.None));

        Assert.Empty(await db.EmulatorTags.ToListAsync());
    }

    private static TagService CreateService(UniEmuDbContext db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var dataCache = new CachedUniEmuDataService(db, cache);
        var scheduleService = new EmulatorScheduleService(
            db,
            dataCache,
            Mock.Of<ISchedulerFactory>(),
            new TagRuntimeStateStore(),
            NullLogger<EmulatorScheduleService>.Instance);

        return new TagService(db, dataCache, scheduleService, new CsxLanguageService());
    }

    private static CreateTagRequest CreateRequest(string inlineScript) => new(
        "Inline tag",
        "inline_tag",
        TagType.Double,
        TagSource.Formula,
        "(computed)",
        new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
        null,
        new TagFormulaConfigDto(null, inlineScript),
        null,
        true,
        null,
        null,
        null);

    private sealed class TagDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private TagDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<TagDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new TagDbFixture(connection, options);
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
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.ScriptFiles.Add(new ScriptFileEntity
            {
                Id = "scr-shared",
                Name = "common.csx",
                Scope = "shared",
                Content = "int Add(int a, int b) => a + b;",
                SizeBytes = 31,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }
    }
}
