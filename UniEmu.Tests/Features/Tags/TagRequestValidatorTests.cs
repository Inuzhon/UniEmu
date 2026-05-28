using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Features.Tags;

namespace UniEmu.Tests.Features.Tags;

public sealed class TagRequestValidatorTests
{
    [Theory]
    [InlineData(" ", "tag", "Имя тега обязательно")]
    [InlineData("Tag", " ", "Ключ тега обязателен")]
    [InlineData("Tag", "bad key", "Ключ тега не должен содержать пробельные символы")]
    public void ValidateCreate_RejectsInvalidIdentity(string name, string key, string expectedMessage)
    {
        var request = CreateRequest(name: name, key: key);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(SpecialParameter.PrgName)]
    [InlineData(SpecialParameter.FrameText)]
    [InlineData(SpecialParameter.ErrorText)]
    [InlineData(SpecialParameter.Message)]
    [InlineData(SpecialParameter.CNCModel)]
    [InlineData(SpecialParameter.FirmwareVersion)]
    [InlineData(SpecialParameter.SerialNumber)]
    [InlineData(SpecialParameter.PLCVersion)]
    [InlineData(SpecialParameter.Subprogram)]
    public void ValidateCreate_RejectsTextSpecialParameter_WhenTypeIsNotString(SpecialParameter specialParameter)
    {
        var request = CreateRequest(type: TagType.Double, specialParameter: specialParameter);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("строковый тип данных", exception.Message);
    }

    [Theory]
    [InlineData(TagType.Double)]
    [InlineData(TagType.String)]
    [InlineData(TagType.Bool)]
    public void ValidateCreate_RejectsFrameNumberSpecialParameter_WhenTypeIsNotInt(TagType type)
    {
        var request = CreateRequest(type: type, specialParameter: SpecialParameter.FrameNum);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("целочисленный тип данных", exception.Message);
    }

    [Theory]
    [InlineData(TagType.String)]
    [InlineData(TagType.Bool)]
    public void ValidateCreate_RejectsFormulaGenerator_ForNonNumericTagTypes(TagType type)
    {
        var request = CreateRequest(type: type, source: TagSource.Generator);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("только для числовых типов", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsStaticPreview_WhenItDoesNotMatchTagType()
    {
        var request = CreateRequest(type: TagType.Int, source: TagSource.Static, preview: "1.5");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Статическое значение тега должно быть целым числом", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsGeneratorDistortionOutsidePercentRange()
    {
        var request = CreateRequest(
            source: TagSource.Generator,
            calc: new TagCalcConfigDto(CalcType.Line, "0", "100", 60, null, null, null, 101));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Искажение (% шума)", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsGeneratorDurationLessThanOne()
    {
        var request = CreateRequest(
            source: TagSource.Generator,
            calc: new TagCalcConfigDto(CalcType.Line, "0", "100", 0, null, null, null, null));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Длительность формулы должна быть больше нуля", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsInvalidTrigger()
    {
        var request = CreateRequest(
            trigger: new TagTriggerDto(TagTriggerMode.Interval, null, null, 0, TagIntervalUnit.Sec));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Интервал вычисления тега должен быть больше нуля", exception.Message);
    }

    [Theory]
    [InlineData(TagType.String)]
    [InlineData(TagType.Bool)]
    public void ValidateCreate_AllowsScriptSources_ForNonNumericTagTypes(TagType type)
    {
        TagRequestValidator.Validate(CreateRequest(type: type, source: TagSource.Script, calc: null));
        TagRequestValidator.Validate(CreateRequest(type: type, source: TagSource.FormulaScript));
    }

    [Theory]
    [InlineData(TagType.String)]
    [InlineData(TagType.Bool)]
    public void ValidateCreate_AllowsStaticScenarioSegments_ForNonNumericTagTypes(TagType type)
    {
        var request = CreateScenarioRequest(type, CalcType.Static);

        TagRequestValidator.Validate(request);
    }

    [Theory]
    [InlineData(TagType.String)]
    [InlineData(TagType.Bool)]
    public void ValidateCreate_RejectsFormulaScenarioSegments_ForNonNumericTagTypes(TagType type)
    {
        var request = CreateScenarioRequest(type, CalcType.Sinusoid);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Сценарий для этого типа данных", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsScenarioSegmentWithInvalidDuration()
    {
        var request = new CreateTagRequest(
            "Scenario",
            "scenario",
            TagType.Double,
            TagSource.Scenario,
            "0",
            CreateTrigger(),
            null,
            null,
            new TagScenarioConfigDto(
                [
                    new TagScenarioSegmentDto(
                        "seg-1",
                        0,
                        new TagCalcConfigDto(CalcType.Static, "0", null, null, null, null, null, null),
                        null),
                ],
                ContinueOnFormulaEnd.Repeat,
                null),
            true,
            null,
            null,
            null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Длительность участка сценария должна быть больше нуля", exception.Message);
    }

    [Fact]
    public void ValidateCreate_RejectsScriptSourceWithoutFormulaConfig()
    {
        var request = CreateRequest(source: TagSource.Script, calc: null, useDefaultFormula: false);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("Для скриптового источника нужен .csx-скрипт", exception.Message);
    }

    [Fact]
    public void ValidateReplace_AppliesTheSameRulesAsCreate()
    {
        var request = new ReplaceTagRequest(
            "Frame",
            "frame",
            TagType.String,
            TagSource.Static,
            "0",
            CreateTrigger(),
            null,
            null,
            null,
            true,
            null,
            SpecialParameter.FrameNum,
            null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TagRequestValidator.Validate(request));

        Assert.Contains("целочисленный тип данных", exception.Message);
    }

    private static CreateTagRequest CreateRequest(
        TagType type = TagType.Double,
        TagSource source = TagSource.Static,
        SpecialParameter? specialParameter = null,
        TagCalcConfigDto? calc = null,
        string name = "Tag",
        string key = "tag",
        string preview = "0",
        TagTriggerDto? trigger = null,
        TagFormulaConfigDto? formula = null,
        bool useDefaultFormula = true) => new(
        name,
        key,
        type,
        source,
        preview,
        trigger ?? CreateTrigger(),
        calc ?? new TagCalcConfigDto(CalcType.Line, "0", "100", 60, null, null, null, null),
        formula ?? (useDefaultFormula && source is (TagSource.Script or TagSource.Formula or TagSource.FormulaScript)
            ? new TagFormulaConfigDto(null, "return 1;")
            : null),
        null,
        true,
        null,
        specialParameter,
        null);

    private static CreateTagRequest CreateScenarioRequest(TagType type, CalcType calcType) => new(
        "Scenario",
        "scenario",
        type,
        TagSource.Scenario,
        "0",
        CreateTrigger(),
        null,
        null,
        new TagScenarioConfigDto(
            [
                new TagScenarioSegmentDto(
                    "seg-1",
                    10,
                    new TagCalcConfigDto(calcType, "0", "100", 10, 1, 10, null, null),
                    null),
            ],
            ContinueOnFormulaEnd.Repeat,
            null),
        true,
        null,
        null,
        null);

    private static TagTriggerDto CreateTrigger() =>
        new(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec);
}
