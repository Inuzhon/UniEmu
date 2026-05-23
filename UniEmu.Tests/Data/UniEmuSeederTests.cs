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
using UniEmu.Runtime.Scripting.Environment;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Data;

public sealed class UniEmuSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesThermalFurnaceDemoDataset()
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
            var scriptIds = scripts.Select(script => script.Id).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(
                [
                    "Furnace_Brazing_03",
                    "Furnace_Carburizing_01",
                    "Furnace_Tempering_02",
                ],
                emulators.Select(emulator => emulator.Name));
            Assert.DoesNotContain(emulators, emulator => emulator.Name.StartsWith("CNC_", StringComparison.OrdinalIgnoreCase));

            foreach (var emulator in emulators)
            {
                var furnaceTags = tags.Where(tag => tag.EmulatorId == emulator.Id).ToList();
                AssertTagSetIsComplete(furnaceTags, scriptIds);
            }

            var carburizingTemperature = tags.Single(tag =>
                tag.EmulatorId == "em-furnace-carburizing-01" &&
                tag.Key == "Temperature");
            var scenario = UniEmuJson.Deserialize<TagScenarioConfigDto>(carburizingTemperature.ScenarioJson)!;

            Assert.Equal(ContinueOnFormulaEnd.Repeat, scenario.ContinueOnFormulaEnd);
            Assert.Contains(scenario.Segments, segment => segment.Label == "Heating");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Soaking");
            Assert.Contains(scenario.Segments, segment => segment.Label == "DoorOpenPartChange");
            Assert.Contains(scenario.Segments, segment => segment.Label == "HighTempSoak");
            Assert.Contains(scenario.Segments, segment => segment.Label == "Cooling");

            Assert.Contains(scripts, script => script.Id == "scr-furnace-math" && script.Name == "furnace/math.csx");
            Assert.Contains(scripts, script => script.Id == "scr-furnace-heater-power" && script.Name == "furnace/heater-power.csx");
            Assert.Contains(scripts, script => script.Id == "scr-furnace-deviation" && script.Name == "furnace/deviation.csx");
            Assert.Contains(scripts, script => script.Id == "scr-furnace-alarm-code" && script.Name == "furnace/alarm-code.csx");
        }
    }

    [Fact]
    public async Task SeedAsync_CreatesRuntimeCompilableFurnaceScripts()
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
            var visibleScripts = scripts.ToDictionary(
                script => TagScriptPath.Normalize(script.Name),
                script => script.Content,
                StringComparer.OrdinalIgnoreCase);
            var environment = new CsxScriptEnvironment();
            var directiveValidator = new CsxScriptDirectiveValidator();
            var securityValidator = new CsxScriptSecurityValidator();

            foreach (var script in scripts)
            {
                var path = TagScriptPath.Normalize(script.Name);
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

    private static void AssertTagSetIsComplete(IReadOnlyList<EmulatorTagEntity> tags, ISet<string> scriptIds)
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
            Assert.Contains(formula!.ScriptId!, scriptIds);
        }
    }
}
