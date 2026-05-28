using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;
using UniEmu.Runtime.Scripting;
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Data;

public sealed class UniEmuSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesIndustrialDemoDataset()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var emulators = await db.Emulators
                .OrderBy(emulator => emulator.Name)
                .ToListAsync();
            var tags = await db.EmulatorTags.ToListAsync();
            var scripts = await db.ScriptFiles.ToListAsync();
            var noneSpecialParameter = UniEmuJson.EnumString(SpecialParameter.None);

            Assert.All(
                tags,
                tag => Assert.False(
                    string.IsNullOrWhiteSpace(tag.SpecialParameter),
                    $"{tag.EmulatorId}/{tag.Key} has empty special parameter."));
            Assert.Contains(tags, tag => tag.SpecialParameter == noneSpecialParameter);

            Assert.Equal(
                [
                    "Индустриальная печь Oven1",
                    "Печь отпуска 2",
                    "Печь пайки 3",
                    "Печь цементации 1",
                    "Портальный фрезерный станок",
                    "Смесительный реактор BR-101",
                    "Токарный станок Turn 200",
                    "Фрезерный центр VMC 650",
                ],
                emulators.Select(emulator => emulator.Name));

            foreach (var emulator in emulators.Where(emulator => emulator.ProtocolId is >= 31 and <= 33))
            {
                var furnaceTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertFurnaceTagSetIsComplete(furnaceTags, scripts, emulator.Id);
            }

            foreach (var emulator in emulators.Where(emulator => emulator.ProtocolId is >= 41 and <= 43))
            {
                var cncTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertCncTagSetIsComplete(cncTags, scripts, emulator.Id);
            }

            foreach (var emulator in emulators.Where(emulator => emulator.ProtocolId == 51))
            {
                var batchTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertBatchReactorTagSetIsComplete(batchTags, scripts, emulator.Id);
            }

            var legacyOven = emulators.Single(emulator => emulator.ProtocolId == 13);
            AssertLegacyOvenTagSetIsComplete(
                tags.Where(tag => tag.EmulatorId == legacyOven.Id).ToList(),
                scripts,
                legacyOven.Id);

            var carburizingEmulatorId = emulators.Single(emulator => emulator.Name == "Печь цементации 1").Id;
            var carburizingTemperature = tags.Single(tag =>
                tag.EmulatorId == carburizingEmulatorId &&
                tag.Key == "Temperature");
            var scenario = UniEmuJson.Deserialize<TagScenarioConfigDto>(carburizingTemperature.ScenarioJson)!;

            Assert.Equal(ContinueOnFormulaEnd.Repeat, scenario.ContinueOnFormulaEnd);
            Assert.Contains(scenario.Segments, segment => segment.Label == "Нагрев");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Выдержка");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Замена детали");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Высокотемпературная выдержка");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Охлаждение");

            Assert.Contains(scripts, script => script.Id == "scr-math" && script.Name == "math.csx");
            Assert.Contains(scripts, script => script.Id == "scr-read-tags" && script.Name == "read-tags.csx");
            Assert.DoesNotContain(scripts, script => script.Name.Contains('/', StringComparison.Ordinal));
            Assert.All(
                scripts.Where(script => script.Id is not "scr-math" and not "scr-read-tags"),
                script =>
                {
                    Assert.Equal(ScriptScope.Emulator, UniEmuJson.EnumValue<ScriptScope>(script.Scope));
                    Assert.False(string.IsNullOrWhiteSpace(script.EmulatorId));
                });

            var programs = await db.CncPrograms
                .OrderBy(program => program.Name)
                .ToListAsync();
            Assert.Equal(
                [
                    "LATHE200_GROOVE_CYCLE.NC",
                    "LATHE200_MAIN.NC",
                    "LATHE200_THREAD_SHAFT.NC",
                    "ROUTER03_NESTING.NC",
                    "ROUTER03_PANEL_LONG.NC",
                    "ROUTER03_SIGN_ENGRAVE.NC",
                    "VMC650_3D_SURFACE_LONG.NC",
                    "VMC650_DRILL_GRID.NC",
                    "VMC650_MAIN.NC",
                ],
                programs.Select(program => program.Name));

            foreach (var emulator in emulators.Where(emulator => emulator.ProtocolId is >= 41 and <= 43))
            {
                var emulatorPrograms = programs.Where(program => program.EmulatorId == emulator.Id).ToList();
                Assert.True(emulatorPrograms.Count >= 3, $"{emulator.Name} has {emulatorPrograms.Count} CNC programs.");
                Assert.All(emulatorPrograms, program => Assert.Equal(CncScope.Emulator, UniEmuJson.EnumValue<CncScope>(program.Scope)));
            }

            var largeProgram = programs.Single(program => program.Name == "VMC650_3D_SURFACE_LONG.NC");
            Assert.True(largeProgram.Content.Split('\n').Length >= 250);
            Assert.All(programs, program => Assert.True(program.SizeBytes > 0));
        }
    }

    [Fact]
    public async Task SeedAsync_CreatesRuntimeCompilableSeedScripts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var scripts = await db.ScriptFiles.ToListAsync();
            var tags = await db.EmulatorTags.ToListAsync();
            var sharedScope = UniEmuJson.EnumString(ScriptScope.Shared);
            var environment = new CsxScriptEnvironment();
            var directiveValidator = new CsxScriptDirectiveValidator();
            var securityValidator = new CsxScriptSecurityValidator();

            foreach (var script in scripts)
            {
                var path = TagScriptPath.Normalize(script.Name);
                var visibleScriptEntities = scripts.Where(candidate =>
                    candidate.Scope == sharedScope ||
                    !string.IsNullOrWhiteSpace(script.EmulatorId) && candidate.EmulatorId == script.EmulatorId);
                var visibleScripts = VisibleScriptResolver.ToContentMap(visibleScriptEntities);
                directiveValidator.ValidateSupportedDirectives(script.Content);
                directiveValidator.DetectLoadCycles(path, visibleScripts);

                var compiledScript = CSharpScript.Create<object?>(
                    script.Content,
                    environment.CreateScriptOptions(path, visibleScripts, typeof(TagScriptGlobals)),
                    typeof(TagScriptGlobals));
                var errors = compiledScript.Compile()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToList();

                Assert.True(
                    errors.Count == 0,
                    $"{script.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

                var securityIssues = securityValidator.Validate(compiledScript.GetCompilation());
                Assert.True(
                    securityIssues.Count == 0,
                    $"{script.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, securityIssues)}");
            }

            foreach (var tag in tags)
            {
                var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
                if (string.IsNullOrWhiteSpace(formula?.InlineScript))
                    continue;

                var path = TagScriptPath.Normalize($"inline/{tag.Id}.csx");
                var visibleScriptEntities = scripts.Where(candidate =>
                    candidate.Scope == sharedScope ||
                    !string.IsNullOrWhiteSpace(tag.EmulatorId) && candidate.EmulatorId == tag.EmulatorId);
                var visibleScripts = new Dictionary<string, string>(VisibleScriptResolver.ToContentMap(visibleScriptEntities), StringComparer.OrdinalIgnoreCase)
                {
                    [path] = formula.InlineScript!,
                };

                directiveValidator.ValidateSupportedDirectives(formula.InlineScript!);
                directiveValidator.DetectLoadCycles(path, visibleScripts);

                var compiledScript = CSharpScript.Create<object?>(
                    formula.InlineScript!,
                    environment.CreateScriptOptions(path, visibleScripts, typeof(TagScriptGlobals)),
                    typeof(TagScriptGlobals));
                var errors = compiledScript.Compile()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToList();

                Assert.True(
                    errors.Count == 0,
                    $"{path}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

                var securityIssues = securityValidator.Validate(compiledScript.GetCompilation());
                Assert.True(
                    securityIssues.Count == 0,
                    $"{path}:{Environment.NewLine}{string.Join(Environment.NewLine, securityIssues)}");
            }
        }
    }

    [Fact]
    public async Task SeedAsync_SharedScriptsDocumentEditorHelperMethods()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var mathScript = await db.ScriptFiles.SingleAsync(script => script.Name == "math.csx");
            var readTagsScript = await db.ScriptFiles.SingleAsync(script => script.Name == "read-tags.csx");

            AssertMethodSummary(
                mathScript.Content,
                "double Clamp(double value, double min, double max)",
                "Ограничивает число заданным минимальным и максимальным значением.");
            AssertMethodSummary(
                mathScript.Content,
                "double ToDouble(object? value, double fallback)",
                "Преобразует значение тега в число double или возвращает запасное значение.");
            AssertMethodSummary(
                mathScript.Content,
                "string ToText(object? value, string fallback)",
                "Преобразует значение тега в строку или возвращает запасное значение.");
            AssertMethodSummary(
                mathScript.Content,
                "bool ToBool(object? value, bool fallback)",
                "Преобразует значение тега в bool или возвращает запасное значение.");
            AssertMethodSummary(
                readTagsScript.Content,
                "double ReadNumber(string key, double fallback)",
                "Читает числовой тег по ключу или возвращает запасное значение.");
            AssertMethodSummary(
                readTagsScript.Content,
                "string ReadText(string key, string fallback)",
                "Читает строковый тег по ключу или возвращает запасное значение.");
            AssertMethodSummary(
                readTagsScript.Content,
                "bool ReadBool(string key, bool fallback)",
                "Читает логический тег по ключу или возвращает запасное значение.");
        }
    }

    [Fact]
    public async Task SeedAsync_UsesRussianScenarioSegmentLabels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var tags = await db.EmulatorTags
                .Include(tag => tag.Emulator)
                .Where(tag =>
                    tag.ScenarioJson != null &&
                    tag.Emulator != null &&
                    tag.Emulator.ProtocolId >= 31 &&
                    tag.Emulator.ProtocolId <= 43)
                .ToListAsync();

            Assert.NotEmpty(tags);

            foreach (var tag in tags)
            {
                var scenario = UniEmuJson.Deserialize<TagScenarioConfigDto>(tag.ScenarioJson)!;
                foreach (var segment in scenario.Segments)
                {
                    Assert.True(
                        Regex.IsMatch(segment.Label ?? string.Empty, @"\p{IsCyrillic}"),
                        $"{tag.Key}/{segment.Id}: '{segment.Label}'");
                }
            }
        }
    }

    [Fact]
    public async Task SeedAsync_BatchReactorScriptsExecuteThroughRuntimeService()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var emulator = await db.Emulators
                .Include(e => e.Tags)
                .SingleAsync(e => e.Name == "Смесительный реактор BR-101");
            var stateStore = new TagRuntimeStateStore();
            var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
            var service = new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache());
            var generator = new TelemetryValueGenerator();
            var timestamp = DateTimeOffset.Parse("2026-05-24T12:00:00Z");
            var scriptBackedTags = emulator.Tags
                .Where(tag => UniEmuJson.EnumValue<TagSource>(tag.Source) is TagSource.Formula or TagSource.Script or TagSource.FormulaScript)
                .OrderBy(tag => tag.Key)
                .ToList();

            Assert.NotEmpty(scriptBackedTags);

            foreach (var tag in scriptBackedTags)
            {
                var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
                var currentValue = source == TagSource.FormulaScript
                    ? generator.GenerateTag(emulator, tag, timestamp).Value
                    : null;

                var value = await GenerateScriptTagWithDiagnosticsAsync(service, emulator, tag, timestamp, currentValue);

                Assert.Equal(tag.Key, value.Key);
                Assert.NotNull(value.Value);
            }
        }
    }

    [Fact]
    public async Task SeedAsync_LegacyOvenControllerScriptUpdatesStaticTags()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();

            await UniEmuSeeder.SeedAsync(db);
        }

        await using (var db = new UniEmuDbContext(options))
        {
            var emulator = await db.Emulators
                .Include(e => e.Tags)
                .SingleAsync(e => e.Name == "Индустриальная печь Oven1");
            var stateStore = new TagRuntimeStateStore();
            var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
            var service = new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache());
            var scriptTag = emulator.Tags.Single(tag => tag.Key == "Script");

            var first = await GenerateScriptTagWithDiagnosticsAsync(
                service,
                emulator,
                scriptTag,
                DateTimeOffset.Parse("2026-05-24T12:00:00Z"));
            var second = await GenerateScriptTagWithDiagnosticsAsync(
                service,
                emulator,
                scriptTag,
                DateTimeOffset.Parse("2026-05-24T12:00:01Z"));

            Assert.Equal(0, first.Value);
            Assert.Equal(1, second.Value);
            Assert.False(scriptTag.Enabled);
            Assert.Equal("1", emulator.Tags.Single(tag => tag.Key == "Count").Preview);
            Assert.Equal("20", emulator.Tags.Single(tag => tag.Key == "Temperature").Preview);
            Assert.Equal("150", emulator.Tags.Single(tag => tag.Key == "Setpoint").Preview);
            Assert.Equal("1", emulator.Tags.Single(tag => tag.Key == "PowerOn").Preview);
            Assert.Equal("132", emulator.Tags.Single(tag => tag.Key == "DowntimeReason").Preview);
        }
    }

    private static async Task<GeneratedTagValue> GenerateScriptTagWithDiagnosticsAsync(
        TagScriptExecutionService service,
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        object? currentValue = null)
    {
        try
        {
            return await service.GenerateScriptTagAsync(emulator, tag, timestamp, CancellationToken.None, currentValue);
        }
        catch (CompilationErrorException ex)
        {
            var diagnostics = string.Join(Environment.NewLine, ex.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"{tag.Key}:{Environment.NewLine}{diagnostics}", ex);
        }
    }

    private static void AssertMethodSummary(string content, string signature, string summary)
    {
        var pattern = $@"/// <summary>\s*/// {Regex.Escape(summary)}\s*/// </summary>\s*{Regex.Escape(signature)}";
        Assert.Matches(pattern, content);
    }

    private static void AssertFurnaceTagSetIsComplete(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<ScriptFileEntity> scripts,
        string emulatorId)
    {
        string[] expectedKeys =
        [
            "Temperature",
            "Setpoint",
            "WorkMode",
            "DoorOpen",
            "HeaterPowerPct",
            "FanSpeedPct",
            "TemperatureDeviation",
            "AlarmCode",
            "ProgramName",
        ];

        foreach (var expectedKey in expectedKeys)
        {
            Assert.Contains(tags, tag => tag.Key == expectedKey);
        }

        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Scenario);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Generator);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Script);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.FormulaScript);

        var workMode = tags.Single(tag => tag.Key == "WorkMode");
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(workMode.Type));
        Assert.NotNull(workMode.SpecialParameter);
        Assert.Equal(SpecialParameter.WorkMode, UniEmuJson.EnumValue<SpecialParameter>(workMode.SpecialParameter!));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(workMode.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "DoorOpenPartChange");

        var doorOpen = tags.Single(tag => tag.Key == "DoorOpen");
        Assert.Equal(TagType.Bool, UniEmuJson.EnumValue<TagType>(doorOpen.Type));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(doorOpen.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "true");

        var fanSpeed = tags.Single(tag => tag.Key == "FanSpeedPct");
        Assert.Equal(CalcType.Sinusoid, UniEmuJson.Deserialize<TagCalcConfigDto>(fanSpeed.CalcJson)!.Type);

        var scriptBackedTags = tags.Where(tag =>
            UniEmuJson.EnumValue<TagSource>(tag.Source) is TagSource.Script or TagSource.FormulaScript);
        foreach (var tag in scriptBackedTags)
        {
            var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
            Assert.NotNull(formula?.ScriptId);
            var script = scripts.SingleOrDefault(script => script.Id == formula!.ScriptId);
            Assert.NotNull(script);
            Assert.Equal(ScriptScope.Emulator, UniEmuJson.EnumValue<ScriptScope>(script!.Scope));
            Assert.Equal(emulatorId, script.EmulatorId);
        }
    }

    private static void AssertCncTagSetIsComplete(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<ScriptFileEntity> scripts,
        string emulatorId)
    {
        string[] expectedKeys =
        [
            "PowerState",
            "ControllerMode",
            "ExecutionState",
            "CycleState",
            "ProgramName",
            "SubprogramName",
            "FrameNumber",
            "FrameText",
            "DoorClosed",
            "FixtureClamped",
            "ServoReady",
            "MachineReadiness",
            "TechnologicalStop",
            "EmergencyStop",
            "MachineX",
            "MachineY",
            "MachineZ",
            "DistanceToGo",
            "SpindleCommandRpm",
            "SpindleActualRpm",
            "SpindleDirection",
            "SpindleLoadPct",
            "VibrationMmS",
            "SpindleTemperatureC",
            "SpindleOverridePct",
            "CommandFeedMmMin",
            "ActualFeedMmMin",
            "FeedOverridePct",
            "RapidOverridePct",
            "ActiveMotionMode",
            "ActiveTool",
            "ToolLifeRemainingPct",
            "AxisLoadPct",
            "CycleTimeSec",
            "AlarmCode",
            "AlarmText",
            "WarningText",
            "CncModel",
            "FirmwareVersion",
            "SerialNumber",
            "PlcVersion",
        ];

        foreach (var expectedKey in expectedKeys)
        {
            Assert.Contains(tags, tag => tag.Key == expectedKey);
        }

        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Scenario);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Generator);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Formula);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Script);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.FormulaScript);
        Assert.DoesNotContain(tags, tag => string.Equals(tag.Source, "Cnc", StringComparison.Ordinal));

        string[] staticCncMetadataKeys =
        [
            "CncModel",
            "FirmwareVersion",
            "SerialNumber",
            "PlcVersion",
        ];
        foreach (var key in staticCncMetadataKeys)
        {
            var tag = tags.Single(tag => tag.Key == key);
            Assert.Equal(TagSource.Static, UniEmuJson.EnumValue<TagSource>(tag.Source));
            Assert.False(string.IsNullOrWhiteSpace(tag.Preview));
        }

        var controllerMode = tags.Single(tag => tag.Key == "ControllerMode");
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(controllerMode.Type));
        Assert.Equal(SpecialParameter.WorkMode, UniEmuJson.EnumValue<SpecialParameter>(controllerMode.SpecialParameter!));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(controllerMode.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "AUTO");

        var executionState = tags.Single(tag => tag.Key == "ExecutionState");
        Assert.Equal(SpecialParameter.SystemState, UniEmuJson.EnumValue<SpecialParameter>(executionState.SpecialParameter!));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(executionState.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "ACTIVE");

        var programName = tags.Single(tag => tag.Key == "ProgramName");
        Assert.Equal(SpecialParameter.PrgName, UniEmuJson.EnumValue<SpecialParameter>(programName.SpecialParameter!));

        var frameNumber = tags.Single(tag => tag.Key == "FrameNumber");
        Assert.Equal(TagType.Int, UniEmuJson.EnumValue<TagType>(frameNumber.Type));
        Assert.Equal(SpecialParameter.FrameNum, UniEmuJson.EnumValue<SpecialParameter>(frameNumber.SpecialParameter!));

        var frameText = tags.Single(tag => tag.Key == "FrameText");
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(frameText.Type));
        Assert.Equal(SpecialParameter.FrameText, UniEmuJson.EnumValue<SpecialParameter>(frameText.SpecialParameter!));

        var spindleActual = tags.Single(tag => tag.Key == "SpindleActualRpm");
        Assert.Equal(CalcType.Sinusoid, UniEmuJson.Deserialize<TagCalcConfigDto>(spindleActual.CalcJson)!.Type);
        Assert.Equal(SpecialParameter.SpindleSpeed, UniEmuJson.EnumValue<SpecialParameter>(spindleActual.SpecialParameter!));

        var actualFeed = tags.Single(tag => tag.Key == "ActualFeedMmMin");
        Assert.Equal(SpecialParameter.FeedRate, UniEmuJson.EnumValue<SpecialParameter>(actualFeed.SpecialParameter!));

        var axisLoad = tags.Single(tag => tag.Key == "AxisLoadPct");
        Assert.Equal(CalcType.Sinusoid, UniEmuJson.Deserialize<TagCalcConfigDto>(axisLoad.CalcJson)!.Type);
        Assert.Equal(SpecialParameter.AxisLoad, UniEmuJson.EnumValue<SpecialParameter>(axisLoad.SpecialParameter!));

        var scriptBackedTags = tags.Where(tag =>
            UniEmuJson.EnumValue<TagSource>(tag.Source) is TagSource.Formula or TagSource.Script or TagSource.FormulaScript);
        foreach (var tag in scriptBackedTags)
        {
            var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
            Assert.NotNull(formula?.ScriptId);
            var script = scripts.SingleOrDefault(script => script.Id == formula!.ScriptId);
            Assert.NotNull(script);
            Assert.Equal(ScriptScope.Emulator, UniEmuJson.EnumValue<ScriptScope>(script!.Scope));
            Assert.Equal(emulatorId, script.EmulatorId);
        }
    }

    private static void AssertBatchReactorTagSetIsComplete(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<ScriptFileEntity> scripts,
        string emulatorId)
    {
        string[] expectedKeys =
        [
            "UnitName",
            "Area",
            "Line",
            "EquipmentModel",
            "ControllerVersion",
            "RecipeName",
            "ProductCode",
            "BatchId",
            "MaterialLot",
            "OperatorId",
            "VesselVolumeL",
            "TargetTemperatureC",
            "TargetPh",
            "BatchPhase",
            "PhaseStep",
            "LevelPct",
            "TemperatureC",
            "PressureBar",
            "AgitatorSpeedRpm",
            "FeedValveOpen",
            "DrainValveOpen",
            "CipActive",
            "ResidenceTimeMin",
            "BatchProgressPct",
            "TemperatureDeviationC",
            "PhEstimate",
            "QualityState",
            "EnergyKw",
            "AlarmCode",
            "AlarmText",
        ];

        foreach (var expectedKey in expectedKeys)
        {
            Assert.Contains(tags, tag => tag.Key == expectedKey);
        }

        Assert.True(tags.Count(tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Static) >= 10);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Scenario);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Generator);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Script);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Formula);
        Assert.Contains(tags, tag => UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.FormulaScript);

        var recipeName = tags.Single(tag => tag.Key == "RecipeName");
        Assert.Equal(TagSource.Static, UniEmuJson.EnumValue<TagSource>(recipeName.Source));
        Assert.Equal(SpecialParameter.PrgName, UniEmuJson.EnumValue<SpecialParameter>(recipeName.SpecialParameter!));

        var batchPhase = tags.Single(tag => tag.Key == "BatchPhase");
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(batchPhase.Type));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(batchPhase.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "Reaction");

        var feedValveOpen = tags.Single(tag => tag.Key == "FeedValveOpen");
        Assert.Equal(TagType.Bool, UniEmuJson.EnumValue<TagType>(feedValveOpen.Type));
        Assert.Contains(
            UniEmuJson.Deserialize<TagScenarioConfigDto>(feedValveOpen.ScenarioJson)!.Segments,
            segment => segment.Calc.Start == "true");

        var savedScriptBackedTags = tags.Where(tag =>
        {
            var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
            return !string.IsNullOrWhiteSpace(formula?.ScriptId);
        }).ToList();
        Assert.True(savedScriptBackedTags.Count >= 4);

        foreach (var tag in savedScriptBackedTags)
        {
            var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
            var script = scripts.SingleOrDefault(script => script.Id == formula!.ScriptId);
            Assert.NotNull(script);
            Assert.Equal(ScriptScope.Emulator, UniEmuJson.EnumValue<ScriptScope>(script!.Scope));
            Assert.Equal(emulatorId, script.EmulatorId);
        }

        var inlineScriptBackedTags = tags.Where(tag =>
        {
            var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(tag.FormulaJson);
            return !string.IsNullOrWhiteSpace(formula?.InlineScript);
        }).ToList();
        Assert.True(inlineScriptBackedTags.Count >= 2);
        Assert.Contains(inlineScriptBackedTags, tag => tag.Key == "PhEstimate");
        Assert.Contains(inlineScriptBackedTags, tag => tag.Key == "EnergyKw");

        var progress = tags.Single(tag => tag.Key == "BatchProgressPct");
        Assert.Equal(TagSource.FormulaScript, UniEmuJson.EnumValue<TagSource>(progress.Source));
        Assert.Equal(CalcType.Line, UniEmuJson.Deserialize<TagCalcConfigDto>(progress.CalcJson)!.Type);

        var qualityState = tags.Single(tag => tag.Key == "QualityState");
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(qualityState.Type));
        Assert.Equal(TagSource.Script, UniEmuJson.EnumValue<TagSource>(qualityState.Source));
    }

    private static void AssertLegacyOvenTagSetIsComplete(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<ScriptFileEntity> scripts,
        string emulatorId)
    {
        string[] expectedKeys =
        [
            "Count",
            "Temperature",
            "Setpoint",
            "ActualParts",
            "PowerOn",
            "Cooling",
            "WorkByProg",
            "Holding",
            "LoadingDSE",
            "DetailComplete",
            "Worker_ID",
            "Worker_Count",
            "DowntimeReason",
            "Script",
            "PrgName",
            "Heating",
            "Overheating",
        ];

        foreach (var expectedKey in expectedKeys)
        {
            Assert.Contains(tags, tag => tag.Key == expectedKey);
        }

        var controller = tags.Single(tag => tag.Key == "Script");
        Assert.Equal(TagSource.Script, UniEmuJson.EnumValue<TagSource>(controller.Source));
        Assert.False(controller.Enabled);

        foreach (var tag in tags.Where(tag => tag.Key != "Script"))
        {
            Assert.Equal(TagSource.Static, UniEmuJson.EnumValue<TagSource>(tag.Source));
            Assert.True(tag.Enabled, $"{tag.Key} should be published.");
        }

        Assert.Equal(TagType.Double, UniEmuJson.EnumValue<TagType>(tags.Single(tag => tag.Key == "Temperature").Type));
        Assert.Equal(TagType.String, UniEmuJson.EnumValue<TagType>(tags.Single(tag => tag.Key == "PrgName").Type));
        Assert.Equal(SpecialParameter.PrgName, UniEmuJson.EnumValue<SpecialParameter>(tags.Single(tag => tag.Key == "PrgName").SpecialParameter!));
        Assert.Equal(SpecialParameter.PartCounter, UniEmuJson.EnumValue<SpecialParameter>(tags.Single(tag => tag.Key == "ActualParts").SpecialParameter!));

        var formula = UniEmuJson.Deserialize<TagFormulaConfigDto>(controller.FormulaJson);
        Assert.Equal("scr-legacy-oven-main", formula?.ScriptId);

        string[] expectedScripts =
        [
            "legacy-oven-constants.csx",
            "legacy-oven-state.csx",
            "legacy-oven-main.csx",
        ];

        foreach (var scriptName in expectedScripts)
        {
            var script = scripts.SingleOrDefault(script => script.Name == scriptName);
            Assert.NotNull(script);
            Assert.Equal(ScriptScope.Emulator, UniEmuJson.EnumValue<ScriptScope>(script!.Scope));
            Assert.Equal(emulatorId, script.EmulatorId);
        }
    }
}
