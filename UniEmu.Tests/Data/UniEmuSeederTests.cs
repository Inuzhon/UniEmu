using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

            Assert.Equal(
                [
                    "CNC_Lathe_Turn_200_02",
                    "CNC_Router_Gantry_03",
                    "CNC_VMC_650_01",
                    "Furnace_Brazing_03",
                    "Furnace_Carburizing_01",
                    "Furnace_Tempering_02",
                ],
                emulators.Select(emulator => emulator.Name));

            foreach (var emulator in emulators.Where(emulator => emulator.Name.StartsWith("Furnace_", StringComparison.Ordinal)))
            {
                var furnaceTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertFurnaceTagSetIsComplete(furnaceTags, scripts, emulator.Id);
            }

            foreach (var emulator in emulators.Where(emulator => emulator.Name.StartsWith("CNC_", StringComparison.Ordinal)))
            {
                var cncTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertCncTagSetIsComplete(cncTags, scripts, emulator.Id);
            }

            var carburizingEmulatorId = emulators.Single(emulator => emulator.Name == "Furnace_Carburizing_01").Id;
            var carburizingTemperature = tags.Single(tag =>
                tag.EmulatorId == carburizingEmulatorId &&
                tag.Key == "Temperature");
            var scenario = UniEmuJson.Deserialize<TagScenarioConfigDto>(carburizingTemperature.ScenarioJson)!;

            Assert.Equal(ContinueOnFormulaEnd.Repeat, scenario.ContinueOnFormulaEnd);
            Assert.Contains(scenario.Segments, segment => segment.Label == "Heating");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Soaking");
            Assert.Contains(scenario.Segments, segment => segment.Label == "DoorOpenPartChange");
            Assert.Contains(scenario.Segments, segment => segment.Label == "HighTempSoak");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Cooling");

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

            foreach (var emulator in emulators.Where(emulator => emulator.Name.StartsWith("CNC_", StringComparison.Ordinal)))
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
        }
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
}
