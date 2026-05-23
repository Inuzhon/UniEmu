using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Common;
using UniEmu.Features.Scripts;
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
    public async Task GenerateScriptTagAsync_MarksStaticTagSideEffectForDeferredFlush()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var flushService = new TagPreviewFlushService(fixture.CreateDbContext, NullLogger<TagPreviewFlushService>.Instance);
        var service = CreateService(db, stateStore, previewFlushService: flushService);
        var (emulator, tag) = await LoadNoTrackingAsync(db, "tg-update-static");

        await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);
        await flushService.FlushAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var setpoint = await db.EmulatorTags.SingleAsync(t => t.Id == "tg-setpoint");
        Assert.Equal("42.57", setpoint.Preview);
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
    public async Task GenerateScriptTagAsync_ExposesStateMetadataAndExecutionCounter()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        stateStore.Set("em-1", "tg-state-metadata", "State metadata", 5.5d, 5.5d, DateTimeOffset.Parse("2026-05-11T09:59:59Z"));
        var service = CreateService(db, stateStore);
        var (emulator, tag) = await LoadAsync(db, "tg-state-metadata");

        var first = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);
        var second = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:01Z"), CancellationToken.None);

        Assert.Equal(1011d, first.Value);
        Assert.Equal(1111d, second.Value);

        var state = await db.ScriptRuntimeStates.SingleAsync(s => s.EmulatorId == "em-1" && s.ScriptKey == "inline:tg-state-metadata");
        Assert.Contains("\"runs\":2", state.ValuesJson);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_AllowsScriptStateRemoveAndClear()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        db.ScriptRuntimeStates.Add(new ScriptRuntimeStateEntity
        {
            Id = "srs-clear",
            EmulatorId = "em-1",
            ScriptKey = "inline:tg-state-clear",
            ValuesJson = """{"stale":10,"other":20}""",
            UpdatedAt = DateTimeOffset.Parse("2026-05-11T09:59:59Z"),
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-state-clear");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal(10, value.Value);
        var state = await db.ScriptRuntimeStates.SingleAsync(s => s.EmulatorId == "em-1" && s.ScriptKey == "inline:tg-state-clear");
        Assert.Equal("{}", state.ValuesJson);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_ExecutesSavedEmulatorScriptThatLoadsSharedHelper()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-shared-helper");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal(25d, value.Value);
        Assert.Equal(25d, value.NumericValue);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_ExecutesComplexSharedAndEmulatorScriptGraph_ForSavedAndInlineTags()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await AddComplexScriptGraphAsync(db);
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, savedTag) = await LoadAsync(db, "tg-complex-catalog");
        var inlineTag = emulator.Tags.Single(t => t.Id == "tg-complex-inline");

        var savedValue = await GenerateScriptTagWithDiagnosticsAsync(
            service,
            emulator,
            savedTag,
            DateTimeOffset.Parse("2026-05-11T10:00:00Z"),
            CancellationToken.None);
        var firstInlineValue = await GenerateScriptTagWithDiagnosticsAsync(
            service,
            emulator,
            inlineTag,
            DateTimeOffset.Parse("2026-05-11T10:00:01Z"),
            CancellationToken.None);
        var secondInlineValue = await GenerateScriptTagWithDiagnosticsAsync(
            service,
            emulator,
            inlineTag,
            DateTimeOffset.Parse("2026-05-11T10:00:02Z"),
            CancellationToken.None);

        Assert.Equal(28.25d, savedValue.Value);
        Assert.Equal(29d, firstInlineValue.Value);
        Assert.Equal(30d, secondInlineValue.Value);

        var savedState = await db.ScriptRuntimeStates.SingleAsync(s => s.ScriptKey == "script:scr-complex-entry");
        var inlineState = await db.ScriptRuntimeStates.SingleAsync(s => s.ScriptKey == "inline:tg-complex-inline");
        Assert.Contains("\"catalogRuns\":1", savedState.ValuesJson);
        Assert.Contains("\"inlineRuns\":2", inlineState.ValuesJson);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_RecomputesSavedScriptTag_WhenLoadedParentScriptChanges()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var dataCache = new CachedUniEmuDataService(db, memoryCache);
        var compiledCache = new CompiledTagScriptCache();
        var executionService = CreateService(db, new TagRuntimeStateStore(), dataCache: dataCache, compiledCache: compiledCache);
        var scriptService = new ScriptService(
            db,
            dataCache,
            new ScopedResourceValidator(db),
            new CsxLanguageService(),
            compiledCache);
        var (emulator, tag) = await LoadAsync(db, "tg-shared-helper");

        var first = await executionService.GenerateScriptTagAsync(
            emulator,
            tag,
            DateTimeOffset.Parse("2026-05-11T10:00:00Z"),
            CancellationToken.None);

        await scriptService.PatchAsync(
            "scr-shared-math",
            new(null, "double ClampDouble(double value, double min, double max) => 31;"),
            CancellationToken.None);

        var second = await executionService.GenerateScriptTagAsync(
            emulator,
            tag,
            DateTimeOffset.Parse("2026-05-11T10:00:01Z"),
            CancellationToken.None);

        Assert.Equal(25d, first.Value);
        Assert.Equal(31d, second.Value);
        Assert.Equal(31d, second.NumericValue);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_AllowsSharedHelperToUseScriptState()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-shared-state");

        var first = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);
        var second = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:01Z"), CancellationToken.None);

        Assert.Equal(10, first.Value);
        Assert.Equal(20, second.Value);

        var state = await db.ScriptRuntimeStates.SingleAsync(s => s.EmulatorId == "em-1" && s.ScriptKey == "script:scr-state-entry");
        Assert.Contains("\"sharedRuns\":2", state.ValuesJson);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_ResolvesRelativeLoadFromSavedEmulatorScriptFolder()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-relative-load");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal(77, value.Value);
    }

    [Fact]
    public async Task GenerateScriptTagAsync_ExecutesScriptWithUsingDirectiveImport()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore());
        var (emulator, tag) = await LoadAsync(db, "tg-using-system-text");

        var value = await service.GenerateScriptTagAsync(emulator, tag, DateTimeOffset.Parse("2026-05-11T10:00:00Z"), CancellationToken.None);

        Assert.Equal("UniEmu:Main emulator", value.Value);
        Assert.Null(value.NumericValue);
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
    public async Task GenerateScriptTagAsync_ThrowsTimeout_WhenCpuBoundScriptDoesNotYield()
    {
        await using var fixture = await ScriptExecutionDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = CreateService(db, new TagRuntimeStateStore(), scriptExecutionTimeout: TimeSpan.FromMilliseconds(100));
        var (emulator, tag) = await LoadAsync(db, "tg-cpu-bound-loop");

        var generationTask = service.GenerateScriptTagAsync(
            emulator,
            tag,
            DateTimeOffset.Parse("2026-05-11T10:00:00Z"),
            CancellationToken.None);
        var completedTask = await Task.WhenAny(generationTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(generationTask, completedTask);
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => generationTask);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        ITagScriptRestOperations? restOperations = null,
        TagPreviewFlushService? previewFlushService = null,
        CachedUniEmuDataService? dataCache = null,
        CompiledTagScriptCache? compiledCache = null,
        TimeSpan? scriptExecutionTimeout = null)
    {
        return new TagScriptExecutionService(
            db,
            dataCache ?? new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions())),
            stateStore,
            compiledCache ?? new CompiledTagScriptCache(),
            restOperations,
            previewFlushService,
            scriptExecutionTimeout);
    }

    private static async Task<GeneratedTagValue> GenerateScriptTagWithDiagnosticsAsync(
        TagScriptExecutionService service,
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service.GenerateScriptTagAsync(emulator, tag, timestamp, cancellationToken);
        }
        catch (CompilationErrorException ex)
        {
            var diagnostics = string.Join(Environment.NewLine, ex.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(diagnostics, ex);
        }
    }

    private static async Task AddComplexScriptGraphAsync(UniEmuDbContext db)
    {
        db.EmulatorTags.AddRange(
            CreateComplexScriptTag("tg-complex-catalog", "Complex catalog", "complex-catalog", "scr-complex-entry"),
            CreateComplexScriptTag(
                "tg-complex-inline",
                "Complex inline",
                "complex-inline",
                inlineScript:
                """
                #load "complex/shared/score.csx"
                #load "complex/machine/offsets.csx"

                public sealed class InlineNormalizer
                {
                    public double Normalize(double value) => value + 0.75;
                }

                UniEmu.Tags.TryGetValue("pressure", out var pressure);
                UniEmu.Tags.TryGetValue("enabled", out var enabled);
                UniEmu.Tags.TryGetValue("label", out var label);

                var runs = UniEmu.State.Get<int>("inlineRuns", 0) + 1;
                UniEmu.State.Set("inlineRuns", runs);

                var score = SharedScoring.Score(
                    (double)pressure!.Value!,
                    (bool)enabled!.Value!,
                    label!.Value!.ToString()!);
                var adjusted = MachineOffset.CreateDefault().Apply(score);

                return new InlineNormalizer().Normalize(adjusted) + runs;
                """));

        db.ScriptFiles.AddRange(
            CreateComplexScriptFile(
                "scr-complex-models",
                "complex/shared/models.csx",
                ScriptScope.Shared,
                null,
                """
                public sealed class LoadWindow
                {
                    public LoadWindow(double minimum, double maximum, double weight)
                    {
                        Minimum = minimum;
                        Maximum = maximum;
                        Weight = weight;
                    }

                    public double Minimum { get; }

                    public double Maximum { get; }

                    public double Weight { get; }

                    public bool Matches(double value) => value >= Minimum && value < Maximum;
                }
                """),
            CreateComplexScriptFile(
                "scr-complex-calibration",
                "complex/shared/calibration.csx",
                ScriptScope.Shared,
                null,
                """
                #load "models.csx"

                public sealed class CalibrationProfile
                {
                    private readonly IReadOnlyList<LoadWindow> windows = new List<LoadWindow>
                    {
                        new LoadWindow(0, 10, 1),
                        new LoadWindow(10, 100, 1.6),
                    };

                    public double Apply(double raw)
                    {
                        var window = windows.FirstOrDefault(candidate => candidate.Matches(raw));
                        return raw * (window?.Weight ?? 1);
                    }

                    public static double Trim(double value) => Math.Round(value, 2);
                }
                """),
            CreateComplexScriptFile(
                "scr-complex-score",
                "complex/shared/score.csx",
                ScriptScope.Shared,
                null,
                """
                #load "calibration.csx"

                public static class SharedScoring
                {
                    public static double Score(double raw, bool enabled, string label)
                    {
                        if (!enabled)
                        {
                            return -1;
                        }

                        var profile = new CalibrationProfile();
                        return CalibrationProfile.Trim(profile.Apply(raw) + label.Length);
                    }
                }
                """),
            CreateComplexScriptFile(
                "scr-complex-units",
                "complex/machine/units.csx",
                ScriptScope.Emulator,
                "em-1",
                """
                public static class UnitConversion
                {
                    public static double Bias() => 0.25;
                }
                """),
            CreateComplexScriptFile(
                "scr-complex-offsets",
                "complex/machine/offsets.csx",
                ScriptScope.Emulator,
                "em-1",
                """
                #load "units.csx"

                public sealed class MachineOffset
                {
                    public MachineOffset(double baseOffset)
                    {
                        BaseOffset = baseOffset;
                    }

                    public double BaseOffset { get; }

                    public double Apply(double value) => value + BaseOffset + UnitConversion.Bias();

                    public static MachineOffset CreateDefault() => new MachineOffset(4);
                }
                """),
            CreateComplexScriptFile(
                "scr-complex-entry",
                "complex/machine/catalog-entry.csx",
                ScriptScope.Emulator,
                "em-1",
                """
                #load "complex/shared/score.csx"
                #load "offsets.csx"

                public sealed class CatalogCalculator
                {
                    public double Calculate(double raw, bool enabled, string label) =>
                        MachineOffset.CreateDefault().Apply(SharedScoring.Score(raw, enabled, label));
                }

                UniEmu.Tags.TryGetValue("pressure", out var pressure);
                UniEmu.Tags.TryGetValue("enabled", out var enabled);
                UniEmu.Tags.TryGetValue("label", out var label);

                var runs = UniEmu.State.Get<int>("catalogRuns", 0) + 1;
                UniEmu.State.Set("catalogRuns", runs);
                var catalogResult = new CatalogCalculator().Calculate((double)pressure!.Value!, (bool)enabled!.Value!, label!.Value!.ToString()!);

                return catalogResult + runs;
                """));

        await db.SaveChangesAsync();
    }

    private static EmulatorTagEntity CreateComplexScriptTag(
        string id,
        string name,
        string key,
        string? scriptId = null,
        string? inlineScript = null)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "em-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Script),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
            FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(scriptId, inlineScript)),
        };
    }

    private static ScriptFileEntity CreateComplexScriptFile(
        string id,
        string name,
        ScriptScope scope,
        string? emulatorId,
        string content)
    {
        return new ScriptFileEntity
        {
            Id = id,
            Name = name,
            Scope = UniEmuJson.EnumString(scope),
            EmulatorId = emulatorId,
            Content = content,
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            UpdatedAt = DateTimeOffset.Parse("2026-05-11T09:30:00Z"),
        };
    }

    private static async Task<(EmulatorEntity Emulator, EmulatorTagEntity Tag)> LoadAsync(UniEmuDbContext db, string tagId)
    {
        var emulator = await db.Emulators
            .Include(e => e.Tags)
            .SingleAsync(e => e.Id == "em-1");
        var tag = emulator.Tags.Single(t => t.Id == tagId);

        return (emulator, tag);
    }

    private static async Task<(EmulatorEntity Emulator, EmulatorTagEntity Tag)> LoadNoTrackingAsync(UniEmuDbContext db, string tagId)
    {
        var emulator = await db.Emulators
            .AsNoTracking()
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
                    "tg-state-metadata",
                    "State metadata",
                    "state-metadata",
                    TagType.Double,
                    """
                    var runs = UniEmu.State.Get<int>("runs", 0);
                    UniEmu.State.Set("runs", runs + 1);

                    return (UniEmu.State.IsRunning ? 1000 : 0)
                        + (runs * 100)
                        + (UniEmu.State.PrevNumericValue == 5.5 ? 10 : 0)
                        + (UniEmu.State.PrevTimestamp.HasValue ? 1 : 0);
                    """),
                CreateScriptTag(
                    "tg-state-clear",
                    "State clear",
                    "state-clear",
                    TagType.Int,
                    """
                    var stale = UniEmu.State.Get<int>("stale", -1);
                    UniEmu.State.Remove("stale");
                    UniEmu.State.Clear();
                    return stale;
                    """),
                CreateScriptTag(
                    "tg-forbidden-api",
                    "Forbidden API",
                    "forbidden-api",
                    TagType.String,
                    "return System.Environment.GetEnvironmentVariable(\"UNIEMU_SECRET\");"),
                CreateScriptTag(
                    "tg-cpu-bound-loop",
                    "CPU bound loop",
                    "cpu-bound-loop",
                    TagType.Int,
                    """
                    while (true)
                    {
                    }
                    """),
                CreateScriptTag(
                    "tg-rest-worker",
                    "Rest worker",
                    "rest-worker",
                    TagType.Int,
                    """
                    var worker = await UniEmu.Rest.GetActiveWorkerAsync();
                    return worker?.Id ?? -1;
                    """),
                CreateSavedScriptTag("tg-shared-helper", "Shared helper", "shared-helper", TagType.Double, "scr-shared-entry"),
                CreateSavedScriptTag("tg-shared-state", "Shared state", "shared-state", TagType.Int, "scr-state-entry"),
                CreateSavedScriptTag("tg-relative-load", "Relative load", "relative-load", TagType.Int, "scr-relative-entry"),
                CreateScriptTag(
                    "tg-using-system-text",
                    "Using System.Text",
                    "using-system-text",
                    TagType.String,
                    """
                    using System.Text;

                    var builder = new StringBuilder();
                    builder.Append("Uni");
                    builder.Append("Emu");
                    builder.Append(":");
                    builder.Append(UniEmu.Emulator.Name);
                    return builder.ToString();
                    """),
                CreateTag("tg-pressure", "Pressure", "pressure", TagType.Double, TagSource.Static, "12.5"),
                CreateTag("tg-enabled", "Enabled", "enabled", TagType.Bool, TagSource.Static, "true"),
                CreateTag("tg-label", "Label", "label", TagType.String, TagSource.Static, "abc"),
                CreateTag("tg-setpoint", "Setpoint", "setpoint", TagType.Double, TagSource.Static, "0", roundDigits: 2));

            db.ScriptFiles.AddRange(
                CreateScriptFile(
                    "scr-shared-math",
                    "shared/math.csx",
                    ScriptScope.Shared,
                    null,
                    "double ClampDouble(double value, double min, double max) => Math.Min(Math.Max(value, min), max);"),
                CreateScriptFile(
                    "scr-shared-state",
                    "shared/state.csx",
                    ScriptScope.Shared,
                    null,
                    """
                    int NextSharedRun(TagScriptStateContext state)
                    {
                        var count = state.Get<int>("sharedRuns", 0) + 1;
                        state.Set("sharedRuns", count);
                        return count;
                    }
                    """),
                CreateScriptFile(
                    "scr-shared-entry",
                    "machine/calc.csx",
                    ScriptScope.Emulator,
                    "em-1",
                    """
                    #load "shared/math.csx"
                    return ClampDouble(40, 0, 25);
                    """),
                CreateScriptFile(
                    "scr-state-entry",
                    "machine/stateful.csx",
                    ScriptScope.Emulator,
                    "em-1",
                    """
                    #load "shared/state.csx"
                    return NextSharedRun(UniEmu.State) * 10;
                    """),
                CreateScriptFile(
                    "scr-relative-entry",
                    "machine/main.csx",
                    ScriptScope.Emulator,
                    "em-1",
                    """
                    #load "helpers/local.csx"
                    return LocalValue();
                    """),
                CreateScriptFile(
                    "scr-relative-helper",
                    "machine/helpers/local.csx",
                    ScriptScope.Emulator,
                    "em-1",
                    "int LocalValue() => 77;"));

            await db.SaveChangesAsync();
        }

        private static EmulatorTagEntity CreateScriptTag(string id, string name, string key, TagType type, string script)
        {
            return CreateTag(id, name, key, type, TagSource.Script, "0", script);
        }

        private static EmulatorTagEntity CreateSavedScriptTag(string id, string name, string key, TagType type, string scriptId)
        {
            return CreateTag(id, name, key, type, TagSource.Script, "0", scriptId: scriptId);
        }

        private static EmulatorTagEntity CreateTag(
            string id,
            string name,
            string key,
            TagType type,
            TagSource source,
            string preview,
            string? script = null,
            string? scriptId = null,
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
                FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(scriptId, script)),
            };
        }

        private static ScriptFileEntity CreateScriptFile(
            string id,
            string name,
            ScriptScope scope,
            string? emulatorId,
            string content)
        {
            return new ScriptFileEntity
            {
                Id = id,
                Name = name,
                Scope = UniEmuJson.EnumString(scope),
                EmulatorId = emulatorId,
                Content = content,
                SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
                UpdatedAt = DateTimeOffset.Parse("2026-05-11T09:00:00Z"),
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
