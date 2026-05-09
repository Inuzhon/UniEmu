using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class TelemetryValueGeneratorTests
{
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
}
