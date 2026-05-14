using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TelemetryValueGeneratorTests
{
    [Theory]
    [MemberData(nameof(GeneratorCalcCases))]
    public void GenerateTag_CalculatesGeneratorCalcType(
        CalcType calcType,
        string? start,
        string? finish,
        int? duration,
        double? amplitude,
        double? period,
        double? curvature,
        double? distortion,
        double elapsedSec,
        double expected)
    {
        var generator = new TelemetryValueGenerator();
        var startedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = startedAt };
        var tag = CreateTag("Generated", "Generated", TagType.Double, TagSource.Generator, preview: "42");
        tag.CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
            calcType,
            start,
            finish,
            duration,
            amplitude,
            period,
            curvature,
            distortion));

        var value = generator.GenerateTag(emulator, tag, startedAt.AddSeconds(elapsedSec));

        Assert.Equal(expected, Assert.IsType<double>(value.Value), precision: 12);
        Assert.Equal(expected, value.NumericValue!.Value, precision: 12);
    }

    public static TheoryData<CalcType, string?, string?, int?, double?, double?, double?, double?, double, double> GeneratorCalcCases()
    {
        return new TheoryData<CalcType, string?, string?, int?, double?, double?, double?, double?, double, double>
        {
            { CalcType.None, null, null, null, null, null, null, null, 5, 42 },
            { CalcType.Text, "not-a-number", null, null, null, null, null, null, 5, 42 },
            { CalcType.Line, "10", "20", 10, null, null, null, null, 5, 15 },
            { CalcType.Line, "10", "20", 10, null, null, null, null, 10, 20 },
            { CalcType.Line, "10", "20", 10, null, null, null, null, 15, 15 },
            { CalcType.Curve, "10", "26", 8, null, null, 2, null, 4, 14 },
            { CalcType.Curve, "10", "26", 8, null, null, 2, null, 12, 14 },
            { CalcType.Sequence, "[10,20,30]", null, 9, null, null, null, null, 4, 20 },
            { CalcType.Sequence, "[10,20,30]", null, 9, null, null, null, null, 9, 30 },
            { CalcType.Sequence, "[10,20,30]", null, 9, null, null, null, null, 10, 10 },
            { CalcType.Sinusoid, "100", null, null, 5, 20, null, null, 5, 105 },
            { CalcType.Square, "100", null, null, 5, 10, null, null, 2.5, 105 },
            { CalcType.Square, "100", null, null, 5, 10, null, null, 7.5, 95 },
            { CalcType.Sawtooth, "100", null, null, 5, 10, null, null, 2.5, 97.5 },
            { CalcType.SquircleEarly, "10", "20", 10, null, null, null, null, 5, 17.5 },
            { CalcType.SquircleEarly, "10", "20", 10, null, null, null, null, 15, 17.5 },
            { CalcType.SquircleLate, "10", "20", 10, null, null, null, null, 5, 12.5 },
            { CalcType.SquircleLate, "10", "20", 10, null, null, null, null, 15, 12.5 },
        };
    }

    [Fact]
    public void GenerateTag_RepeatsLineGeneratorAfterInclusiveDurationBoundary()
    {
        var generator = new TelemetryValueGenerator();
        var startedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = startedAt };
        var tag = CreateTag("Generated", "Generated", TagType.Double, TagSource.Generator, preview: "0");
        tag.CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
            CalcType.Line,
            Start: "0",
            Finish: "100",
            Duration: 60,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null));

        var beforeBoundary = generator.GenerateTag(emulator, tag, startedAt.AddSeconds(59));
        var atBoundary = generator.GenerateTag(emulator, tag, startedAt.AddSeconds(60));
        var afterBoundary = generator.GenerateTag(emulator, tag, startedAt.AddSeconds(61));

        Assert.Equal(98.33333333333333, Assert.IsType<double>(beforeBoundary.Value), precision: 12);
        Assert.Equal(100d, atBoundary.Value);
        Assert.Equal(1.6666666666666667, Assert.IsType<double>(afterBoundary.Value), precision: 12);
    }

    [Fact]
    public void GenerateTag_KeepsLineGeneratorFlat_WhenStartAndFinishAreEqual()
    {
        var generator = new TelemetryValueGenerator();
        var startedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = startedAt };
        var tag = CreateTag("Generated", "Generated", TagType.Double, TagSource.Generator, preview: "0");
        tag.CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
            CalcType.Line,
            Start: "50",
            Finish: "50",
            Duration: 60,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null));

        foreach (var elapsedSec in new[] { 0, 30, 60, 61, 120 })
        {
            var value = generator.GenerateTag(emulator, tag, startedAt.AddSeconds(elapsedSec));

            Assert.Equal(50d, value.Value);
            Assert.Equal(50d, value.NumericValue);
        }
    }

    [Fact]
    public void GenerateTag_CalculatesRandomGeneratorWithinConfiguredBounds()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Generated", "Generated", TagType.Double, TagSource.Generator, preview: "0");
        tag.CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
            CalcType.Random,
            Start: "20",
            Finish: "10",
            Duration: null,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null));

        for (var i = 0; i < 20; i++)
        {
            var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:00Z").AddSeconds(i));

            var numericValue = Assert.IsType<double>(value.Value);
            Assert.InRange(numericValue, 10, 20);
            Assert.Equal(numericValue, value.NumericValue);
        }
    }

    [Fact]
    public void GenerateTag_ConvertsStaticBoolPreviewToBooleanAndNumericValue()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Power", "PowerOn", TagType.Bool, TagSource.Static, preview: "true");

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:01Z"));

        Assert.Equal("PowerOn", value.Key);
        Assert.Equal("Power", value.Name);
        Assert.Equal(true, value.Value);
        Assert.Equal(1, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_InterpolatesLineGeneratorFromStartedAt()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Temperature", "Temp", TagType.Double, TagSource.Generator, preview: "0");
        tag.RoundDigits = 2;
        tag.CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
            CalcType.Line,
            Start: "10.111",
            Finish: "20.999",
            Duration: 10,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:05Z"));

        Assert.Equal(15.56, value.Value);
        Assert.Equal(15.56, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_DoesNotRoundDouble_WhenRoundDigitsIsNull()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1" };
        var tag = CreateTag("Temperature", "Temp", TagType.Double, TagSource.Static, preview: "12.3456");

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:00Z"));

        Assert.Equal(12.3456, value.Value);
        Assert.Equal(12.3456, value.NumericValue);
    }

    [Fact]
    public void Generate_ExcludesNonNumericStringValuesFromTelemetryDictionary()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1" };
        var tags = new[]
        {
            CreateTag("Program", "PrgName", TagType.String, TagSource.Static, preview: "main.nc"),
            CreateTag("Feed", "FeedRate", TagType.Double, TagSource.Static, preview: "120.5"),
        };

        var values = generator.Generate(emulator, tags, DateTimeOffset.Parse("2026-05-09T10:00:00Z"));

        Assert.DoesNotContain("PrgName", values.Keys);
        Assert.Equal(120.5, values["FeedRate"]);
    }

    [Fact]
    public void GenerateTag_RepeatsScenario_WhenConfiguredToRepeatAfterTotalDuration()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "0");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                new TagScenarioSegmentDto(
                    "seg-1",
                    Duration: 10,
                    new TagCalcConfigDto(
                        CalcType.Line,
                        Start: "0",
                        Finish: "100",
                        Duration: 10,
                        Amplitude: null,
                        Period: null,
                        Curvature: null,
                        Distortion: null),
                    Label: "Ramp"),
            ],
            ContinueOnFormulaEnd.Repeat,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:15Z"));

        Assert.Equal(50d, value.Value);
        Assert.Equal(50d, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_CalculatesSingleSegmentScenarioWithCurve()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "0");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                CreateScenarioSegment("curve", 8, CalcType.Curve, start: "10", finish: "26", curvature: 2),
            ],
            ContinueOnFormulaEnd.Stretch,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:04Z"));

        Assert.Equal(14d, value.Value);
        Assert.Equal(14d, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_CalculatesSecondSegmentScenarioFormula()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "0");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                CreateScenarioSegment("line", 10, CalcType.Line, start: "0", finish: "100"),
                CreateScenarioSegment("sine", 20, CalcType.Sinusoid, start: "50", amplitude: 10, period: 20),
            ],
            ContinueOnFormulaEnd.Stretch,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:15Z"));

        Assert.Equal(60d, Assert.IsType<double>(value.Value), precision: 12);
        Assert.Equal(60d, value.NumericValue!.Value, precision: 12);
    }

    [Fact]
    public void GenerateTag_CalculatesThirdSegmentScenarioFormula()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "0");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                CreateScenarioSegment("line", 5, CalcType.Line, start: "0", finish: "10"),
                CreateScenarioSegment("square", 10, CalcType.Square, start: "100", amplitude: 5, period: 10),
                CreateScenarioSegment("sequence", 9, CalcType.Sequence, start: "[3,6,9]"),
            ],
            ContinueOnFormulaEnd.Stretch,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:19Z"));

        Assert.Equal(6d, value.Value);
        Assert.Equal(6d, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_StretchesScenarioToLastSegmentValueAfterTotalDuration()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "0");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                CreateScenarioSegment("line", 5, CalcType.Line, start: "0", finish: "10"),
                CreateScenarioSegment("squircle-late", 10, CalcType.SquircleLate, start: "10", finish: "50"),
            ],
            ContinueOnFormulaEnd.Stretch,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:30Z"));

        Assert.Equal(50d, value.Value);
        Assert.Equal(50d, value.NumericValue);
    }

    [Fact]
    public void GenerateTag_ReturnsZeroForScenarioAfterTotalDuration_WhenConfiguredToZero()
    {
        var generator = new TelemetryValueGenerator();
        var emulator = new EmulatorEntity { Id = "emu-1", StartedAt = DateTimeOffset.Parse("2026-05-09T10:00:00Z") };
        var tag = CreateTag("Load", "Load", TagType.Double, TagSource.Scenario, preview: "99");
        tag.ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
            [
                CreateScenarioSegment("line", 5, CalcType.Line, start: "10", finish: "20"),
            ],
            ContinueOnFormulaEnd.Zero,
            StartValue: null));

        var value = generator.GenerateTag(emulator, tag, DateTimeOffset.Parse("2026-05-09T10:00:10Z"));

        Assert.Equal(0d, value.Value);
        Assert.Equal(0d, value.NumericValue);
    }

    private static EmulatorTagEntity CreateTag(
        string name,
        string key,
        TagType type,
        TagSource source,
        string preview)
    {
        return new EmulatorTagEntity
        {
            Id = $"{key}-id",
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(type),
            Source = UniEmuJson.EnumString(source),
            Preview = preview,
        };
    }

    private static TagScenarioSegmentDto CreateScenarioSegment(
        string id,
        double duration,
        CalcType type,
        string? start = null,
        string? finish = null,
        double? amplitude = null,
        double? period = null,
        double? curvature = null,
        double? distortion = null)
    {
        return new TagScenarioSegmentDto(
            id,
            duration,
            new TagCalcConfigDto(
                type,
                start,
                finish,
                Duration: (int)duration,
                amplitude,
                period,
                curvature,
                distortion),
            Label: id);
    }
}
