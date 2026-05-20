using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Tags;

/// <summary>
/// Проверяет совместимость полей конфигурации тега до сохранения.
/// </summary>
public static class TagRequestValidator
{
    private static readonly HashSet<SpecialParameter> StringSpecialParameters =
    [
        SpecialParameter.PrgName,
        SpecialParameter.FrameText,
        SpecialParameter.ErrorText,
        SpecialParameter.Message,
        SpecialParameter.CNCModel,
        SpecialParameter.FirmwareVersion,
        SpecialParameter.SerialNumber,
        SpecialParameter.PLCVersion,
        SpecialParameter.Subprogram,
    ];

    private static readonly HashSet<CalcType> GeneratorCalcTypes =
    [
        CalcType.Line,
        CalcType.Curve,
        CalcType.Sequence,
        CalcType.Random,
        CalcType.Sinusoid,
        CalcType.Square,
        CalcType.Sawtooth,
        CalcType.SquircleEarly,
        CalcType.SquircleLate,
    ];

    /// <summary>
    /// Проверяет запрос создания тега.
    /// </summary>
    /// <param name="request">Запрос создания тега.</param>
    public static void Validate(CreateTagRequest request)
    {
        Validate(
            request.Type,
            request.Source,
            request.Calc,
            request.Scenario,
            request.SpecialParameter);
    }

    /// <summary>
    /// Проверяет запрос полной замены тега.
    /// </summary>
    /// <param name="request">Запрос замены тега.</param>
    public static void Validate(ReplaceTagRequest request)
    {
        Validate(
            request.Type,
            request.Source,
            request.Calc,
            request.Scenario,
            request.SpecialParameter);
    }

    private static void Validate(
        TagType type,
        TagSource source,
        TagCalcConfigDto? calc,
        TagScenarioConfigDto? scenario,
        SpecialParameter? specialParameter)
    {
        ValidateSpecialParameter(type, specialParameter);
        ValidateSource(type, source, calc, scenario);
    }

    private static void ValidateSpecialParameter(TagType type, SpecialParameter? specialParameter)
    {
        if (specialParameter is null or SpecialParameter.None)
        {
            return;
        }

        if (StringSpecialParameters.Contains(specialParameter.Value) && type != TagType.String)
        {
            throw new InvalidOperationException(
                $"Спецпараметр {specialParameter} поддерживает только строковый тип данных.");
        }

        if (specialParameter == SpecialParameter.FrameNum && type != TagType.Int)
        {
            throw new InvalidOperationException(
                "Спецпараметр FrameNum поддерживает только целочисленный тип данных.");
        }
    }

    private static void ValidateSource(
        TagType type,
        TagSource source,
        TagCalcConfigDto? calc,
        TagScenarioConfigDto? scenario)
    {
        if (source == TagSource.Generator)
        {
            if (!IsNumeric(type))
            {
                throw new InvalidOperationException(
                    "Генератор по формуле доступен только для числовых типов данных.");
            }

            ValidateGeneratorCalc(calc);
            return;
        }

        if (source == TagSource.FormulaScript)
        {
            ValidateGeneratorCalc(calc);
            return;
        }

        if (source == TagSource.Scenario)
        {
            ValidateScenario(type, scenario);
        }
    }

    private static void ValidateGeneratorCalc(TagCalcConfigDto? calc)
    {
        if (calc is null)
        {
            throw new InvalidOperationException("Для генератора по формуле нужна формула расчета.");
        }

        if (!GeneratorCalcTypes.Contains(calc.Type))
        {
            throw new InvalidOperationException($"Формула расчета {calc.Type} недоступна для генератора.");
        }
    }

    private static void ValidateScenario(TagType type, TagScenarioConfigDto? scenario)
    {
        if (scenario is null || scenario.Segments.Count == 0)
        {
            throw new InvalidOperationException("Сценарий должен содержать хотя бы один участок.");
        }

        foreach (var segment in scenario.Segments)
        {
            if (segment.Duration <= 0)
            {
                throw new InvalidOperationException("Длительность участка сценария должна быть больше нуля.");
            }

            if (IsNumeric(type))
            {
                if (segment.Calc.Type != CalcType.Static && !GeneratorCalcTypes.Contains(segment.Calc.Type))
                {
                    throw new InvalidOperationException(
                        $"Формула расчета {segment.Calc.Type} недоступна для числового сценария.");
                }

                continue;
            }

            if (segment.Calc.Type != CalcType.Static)
            {
                throw new InvalidOperationException(
                    "Сценарий для этого типа данных поддерживает только статические значения.");
            }
        }
    }

    private static bool IsNumeric(TagType type) => type is TagType.Int or TagType.Double;
}
