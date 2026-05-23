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

public sealed class TagServiceScriptValidationTests
{
    [Fact]
    public async Task CreateAsync_RejectsDuplicateName_WithinSameEmulatorIgnoringCaseAndTrim()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateExistingTag("tg-existing", "Test", "test_key"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("em-1", CreateRequest("return 1;", name: " test "), CancellationToken.None));

        Assert.Equal("Тег с таким именем уже существует в этом эмуляторе.", exception.Message);
        Assert.Equal(1, await db.EmulatorTags.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateKey_WithinSameEmulatorIgnoringCaseAndTrim()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateExistingTag("tg-existing", "Existing", "Test_Key"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("em-1", CreateRequest("return 1;", key: " test_key "), CancellationToken.None));

        Assert.Equal("Тег с таким ключом уже существует в этом эмуляторе.", exception.Message);
        Assert.Equal(1, await db.EmulatorTags.CountAsync());
    }

    [Fact]
    public async Task ReplaceAsync_RejectsDuplicateNameOrKey_ButIgnoresCurrentTag()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateExistingTag("tg-current", "Current", "current_key"));
        db.EmulatorTags.Add(CreateExistingTag("tg-other", "Other", "other_key"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ReplaceAsync(
            "em-1",
            "tg-current",
            CreateReplaceRequest(" current ", " current_key "),
            CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReplaceAsync(
                "em-1",
                "tg-current",
                CreateReplaceRequest(" other ", "current_key"),
                CancellationToken.None));

        Assert.Equal("Тег с таким именем уже существует в этом эмуляторе.", exception.Message);

        exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReplaceAsync(
                "em-1",
                "tg-current",
                CreateReplaceRequest("Current", " other_key "),
                CancellationToken.None));

        Assert.Equal("Тег с таким ключом уже существует в этом эмуляторе.", exception.Message);
    }

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

    [Fact]
    public async Task CreateAsync_RejectsInlineScript_WhenIntTagReturnsString()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<CsxScriptValidationException>(() =>
            service.CreateAsync("em-1", CreateRequest("return \"not an int\";", TagType.Int), CancellationToken.None));

        Assert.Contains(exception.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "CS0029");
        Assert.Empty(await db.EmulatorTags.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_UsesEmulatorScopedLoadedScript_WhenSharedScriptHasSamePath()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var shared = await db.ScriptFiles.SingleAsync(script => script.Id == "scr-shared");
        shared.Content = "string Value() => \"bad\";";
        db.ScriptFiles.Add(new ScriptFileEntity
        {
            Id = "scr-local-common",
            Name = "common.csx",
            Scope = "emulator",
            EmulatorId = "em-1",
            Content = "double Value() => 2;",
            SizeBytes = 21,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            "em-1",
            CreateRequest("#load \"common.csx\"\nreturn Value();"),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal("Inline tag", created.Name);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidTagCompatibility_BeforeSaving()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                "em-1",
                CreateStaticRequest(TagType.String, SpecialParameter.FrameNum),
                CancellationToken.None));

        Assert.Contains("целочисленный тип данных", exception.Message);
        Assert.Empty(await db.EmulatorTags.ToListAsync());
    }

    [Fact]
    public async Task ReplaceAsync_RejectsInvalidTagCompatibility_BeforeSavingChanges()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.EmulatorTags.Add(CreateExistingTag("tg-current", "Current", "current_key"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReplaceAsync(
                "em-1",
                "tg-current",
                CreateInvalidGeneratorReplaceRequest(),
                CancellationToken.None));

        Assert.Contains("только для числовых типов", exception.Message);

        var entity = await db.EmulatorTags.SingleAsync();
        Assert.Equal(UniEmuJson.EnumString(TagSource.Static), entity.Source);
        Assert.Equal(UniEmuJson.EnumString(TagType.Double), entity.Type);
    }

    [Fact]
    public async Task CreateAsync_NormalizesScenarioOnceTrigger_ToInterval()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync("em-1", CreateScenarioRequest(), CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(TagTriggerMode.Interval, created.Trigger.Mode);
        Assert.Equal(1, created.Trigger.IntervalValue);
        Assert.Equal(TagIntervalUnit.Sec, created.Trigger.IntervalUnit);
        Assert.Null(created.Trigger.Event);

        var entity = await db.EmulatorTags.SingleAsync();
        var storedTrigger = UniEmuJson.Deserialize<TagTriggerDto>(entity.TriggerJson);
        Assert.NotNull(storedTrigger);
        Assert.Equal(TagTriggerMode.Interval, storedTrigger.Mode);
        Assert.Equal(1, storedTrigger.IntervalValue);
        Assert.Equal(TagIntervalUnit.Sec, storedTrigger.IntervalUnit);
        Assert.Null(storedTrigger.Event);
    }

    [Fact]
    public async Task CreateAsync_PreservesScenarioOnStopTrigger()
    {
        await using var fixture = await TagDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            "em-1",
            CreateScenarioRequest(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStop, null, null, null)),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(TagTriggerMode.Once, created.Trigger.Mode);
        Assert.Equal(TagTriggerEvent.OnStop, created.Trigger.Event);
    }

    private static TagService CreateService(UniEmuDbContext db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var dataCache = new CachedUniEmuDataService(db, cache);
        var flushService = new TagPreviewFlushService(() => db, NullLogger<TagPreviewFlushService>.Instance);
        var scheduleService = new EmulatorScheduleService(
            db,
            dataCache,
            Mock.Of<ISchedulerFactory>(),
            new TagRuntimeStateStore(),
            flushService,
            NullLogger<EmulatorScheduleService>.Instance,
            Options.Create(new UniEmuOptions()),
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, new TagRuntimeStateStore(), new CompiledTagScriptCache()),
            new RuntimeUpdateService(Mock.Of<IRuntimeUpdateBroadcaster>()));

        return new TagService(db, dataCache, scheduleService, new CsxLanguageService());
    }

    private static CreateTagRequest CreateRequest(
        string inlineScript,
        TagType type = TagType.Double,
        string name = "Inline tag",
        string key = "inline_tag") => new(
        name,
        key,
        type,
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

    private static CreateTagRequest CreateStaticRequest(TagType type, SpecialParameter? specialParameter) => new(
        "Static tag",
        "static_tag",
        type,
        TagSource.Static,
        "0",
        new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
        null,
        null,
        null,
        true,
        null,
        specialParameter,
        null);

    private static ReplaceTagRequest CreateReplaceRequest(string name, string key) => new(
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

    private static ReplaceTagRequest CreateInvalidGeneratorReplaceRequest() => new(
        "Current",
        "current_key",
        TagType.Bool,
        TagSource.Generator,
        "false",
        new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec),
        new TagCalcConfigDto(CalcType.Sinusoid, "0", null, null, 1, 10, null, null),
        null,
        null,
        true,
        null,
        null,
        null);

    private static EmulatorTagEntity CreateExistingTag(string id, string name, string key) => new()
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

    private static CreateTagRequest CreateScenarioRequest(TagTriggerDto? trigger = null) => new(
        "Scenario tag",
        "scenario_tag",
        TagType.Double,
        TagSource.Scenario,
        "(scenario)",
        trigger ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
        null,
        null,
        new TagScenarioConfigDto(
            [
                new TagScenarioSegmentDto(
                    "line-up",
                    10,
                    new TagCalcConfigDto(CalcType.Line, "0", "100", 10, null, null, null, null),
                    "Line up"),
            ],
            ContinueOnFormulaEnd.Repeat,
            StartValue: null),
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
