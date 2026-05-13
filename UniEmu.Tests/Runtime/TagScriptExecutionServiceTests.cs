using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Runtime;

public sealed class TagScriptExecutionServiceTests
{
    [Fact]
    public async Task GenerateScriptTagAsync_ExposesPreviousCurrentTagValue()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        stateStore.Set("em-1", "tg-previous", "Previous", 7d, 7d, DateTimeOffset.Parse("2026-05-11T10:00:00Z"));
        var service = CreateService(db, stateStore);
        var (emulator, tag) = await LoadAsync(db, "tg-previous");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:01Z"), CancellationToken.None);

        Assert.Equal(7d, value.Value);
        Assert.Equal(7d, value.NumericValue);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_CalculatesFromOtherTagsWithTypedValues()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-complex");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal(28d, value.Value);
        Assert.Equal(28d, value.NumericValue);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_CanUpdateStaticTagAndUseUpdatedValue()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var service = CreateService(db, stateStore);
        var (emulator, tag) = await LoadAsync(db, "tg-update-static");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal(42.57d, value.Value);

        var setpoint = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-setpoint");
        Assert.Equal("42.57", setpoint.Preview);
        Assert.True(stateStore.TryGet("em-1", "tg-setpoint", out var runtimeValue));
        Assert.Equal(42.57d, runtimeValue.Value);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_PersistsScriptStateBetweenRuns()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-stateful");

        var first = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);
        var second = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:01Z"), CancellationToken.None);

        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);

        var state = await db.ScriptRuntimeStates.SingleAsync(s => s.EmulatorId == "em-1" && s.ScriptKey == "inline:tg-stateful");
        Assert.Contains("\"count\":2", state.ValuesJson);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_BlocksForbiddenRuntimeApi()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-forbidden-api");

        var exception = await Assert.ThrowsAsync<CsxScriptValidationException>(() =>
            service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None));

        Assert.Contains(exception.Diagnostics, diagnostic =>
            diagnostic.Severity == CsxDiagnosticSeverity.Error && diagnostic.Code == "SEC003");
    }

    [Fact]
    public async Task GenerateScriptTagAsync_CanAwaitRestWorkerOperation()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(
            db,
            new TagRuntimeStateStore(),
            new FakeRestOperations(new Worker { Id = 321, Name = "Active", Status = "Ready", IsActive = true }));
        var (emulator, tag) = await LoadAsync(db, "tg-rest-worker");

        var value = await service.GenerateScriptTagAsync(
            emulator,
            tag,
            DateTimeOffset.Parse("2026-05-11T10:00:00Z"),
            CancellationToken.None);

        Assert.Equal(321, value.Value);
        Assert.Equal(321d, value.NumericValue);
    }

    private static TagScriptExecutionService CreateService(
        UniEmuDbContext db,
        TagRuntimeStateStore stateStore,
        ITagScriptRestOperations? restOperations = null)
    {
        return new TagScriptExecutionService(
            db,
            new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions())),
            stateStore,
            new CompiledTagScriptCache(),
            restOperations);
    }

    private static async Task<(EmulatorEntity Emulator, EmulatorTagEntity Tag)> LoadAsync(UniEmuDbContext db, string tagId)
    {
        var emulator = await db.Emulators
            .Include(e => e.Tags)
            .SingleAsync(e => e.Id == "em-1");
        var tag = emulator.Tags.Single(t => t.Id == tagId);

        return (emulator, tag);
    }

    private sealed class ScriptExecutionDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private ScriptExecutionDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<ScriptExecutionDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new ScriptExecutionDbFixture(connection, options);
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
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.EmulatorTags.AddRange(
                CreateScriptTag("tg-previous", "Previous", "previous", TagType.Double, "return UniEmu.Tag.Value;"),
                CreateScriptTag(
                    "tg-complex",
                    "Complex",
                    "complex",
                    TagType.Double,
                    """
                    var pressureOk = UniEmu.Tags.TryGetValue("pressure", out var pressure) && pressure.Type == TagScriptValueType.Double;
                    var enabledOk = UniEmu.Tags.TryGetValue("enabled", out var enabled) && enabled.Type == TagScriptValueType.Bool;
                    var labelOk = UniEmu.Tags.TryGetValue("label", out var label) && label.Type == TagScriptValueType.String;

                    return pressureOk && enabledOk && labelOk && (bool)enabled!.Value!
                        ? (double)pressure!.Value! * 2 + label!.Value!.ToString()!.Length
                        : -100;
                    """),
                CreateScriptTag(
                    "tg-update-static",
                    "Update static",
                    "update-static",
                    TagType.Double,
                    """
                    UniEmu.Tags.TrySetValue("setpoint", 42.567);
                    return UniEmu.Tags.TryGetValue("setpoint", out var setpoint)
                        ? setpoint!.Value
                        : -1;
                    """),
                CreateScriptTag(
                    "tg-stateful",
                    "Stateful",
                    "stateful",
                    TagType.Int,
                    """
                    var count = UniEmu.State.Get<int>("count", 0) + 1;
                    UniEmu.State.Set("count", count);
                    return count;
                    """),
                CreateScriptTag(
                    "tg-forbidden-api",
                    "Forbidden API",
                    "forbidden-api",
                    TagType.String,
                    "return System.Environment.GetEnvironmentVariable(\"UNIEMU_SECRET\");"),
                CreateScriptTag(
                    "tg-rest-worker",
                    "Rest worker",
                    "rest-worker",
                    TagType.Int,
                    """
                    var worker = await UniEmu.Rest.GetActiveWorkerAsync();
                    return worker?.Id ?? -1;
                    """),
                CreateTag("tg-pressure", "Pressure", "pressure", TagType.Double, TagSource.Static, "12.5"),
                CreateTag("tg-enabled", "Enabled", "enabled", TagType.Bool, TagSource.Static, "true"),
                CreateTag("tg-label", "Label", "label", TagType.String, TagSource.Static, "abc"),
                CreateTag("tg-setpoint", "Setpoint", "setpoint", TagType.Double, TagSource.Static, "0", roundDigits: 2));

            await db.SaveChangesAsync();
        }

        private static EmulatorTagEntity CreateScriptTag(string id, string name, string key, TagType type, string script)
        {
            return CreateTag(id, name, key, type, TagSource.Script, "0", script);
        }

        private static EmulatorTagEntity CreateTag(
            string id,
            string name,
            string key,
            TagType type,
            TagSource source,
            string preview,
            string? script = null,
            int? roundDigits = null)
        {
            return new EmulatorTagEntity
            {
                Id = id,
                EmulatorId = "em-1",
                Name = name,
                Key = key,
                Type = UniEmuJson.EnumString(type),
                Source = UniEmuJson.EnumString(source),
                Preview = preview,
                RoundDigits = roundDigits,
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            };
        }
    }

    private sealed class FakeRestOperations(Worker activeWorker) : ITagScriptRestOperations
    {
        public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Worker?>(activeWorker.Id == workerId ? activeWorker : null);
        }

        public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<Worker?>(activeWorker);
        }

        public Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(RestCallResult.Ok());
        }
    }
}
