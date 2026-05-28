using System.Globalization;
using System.Text.Json;
using Quartz;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;

namespace UniEmu.Features.Tags;

/// <summary>
/// Проверяет совместимость полей конфигурации тега до сохранения.
/// </summary>
public static class TagRequestValidator
{
    private const int MaxTagNameLength = 200;
    private const int MaxTagKeyLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const int MaxTextValueLength = 2000;
    private const int MaxScriptIdLength = 64;
    private const int MaxInlineScriptLength = 200_000;
    private const int MaxCalcDurationSeconds = 86_400;
    private const int MaxScenarioSegments = 200;
    private const int MaxScenarioTotalDurationSeconds = 604_800;
    private const int MaxSegmentLabelLength = 120;
    private const double MaxNumericMagnitude = 1_000_000_000d;
    private const double MaxCurvature = 20d;

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

    private static readonly HashSet<CalcType> DurationCalcTypes =
    [
        CalcType.Line,
        CalcType.Curve,
        CalcType.Sequence,
        CalcType.SquircleEarly,
        CalcType.SquircleLate,
    ];

    private static readonly HashSet<CalcType> RangeCalcTypes =
    [
        CalcType.Line,
        CalcType.Curve,
        CalcType.Random,
        CalcType.SquircleEarly,
        CalcType.SquircleLate,
    ];

    private static readonly HashSet<CalcType> WaveCalcTypes =
    [
        CalcType.Sinusoid,
        CalcType.Square,
        CalcType.Sawtooth,
    ];

    /// <summary>
    /// Проверяет запрос создания тега.
    /// </summary>
    /// <param name="request">Запрос создания тега.</param>
    public static void Validate(CreateTagRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(
            request.Name,
            request.Key,
            request.Type,
            request.Source,
            request.Preview,
            request.Trigger,
            request.Calc,
            request.Formula,
            request.Scenario,
            request.SpecialParameter,
            request.Description);
    }

    /// <summary>
    /// Проверяет запрос полной замены тега.
    /// </summary>
    /// <param name="request">Запрос замены тега.</param>
    public static void Validate(ReplaceTagRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(
            request.Name,
            request.Key,
            request.Type,
            request.Source,
            request.Preview,
            request.Trigger,
            request.Calc,
            request.Formula,
            request.Scenario,
            request.SpecialParameter,
            request.Description);
    }

    private static void Validate(
        string? name,
        string? key,
        TagType type,
        TagSource source,
        string? preview,
        TagTriggerDto? trigger,
        TagCalcConfigDto? calc,
        TagFormulaConfigDto? formula,
        TagScenarioConfigDto? scenario,
        SpecialParameter? specialParameter,
        string? description)
    {
        ValidateIdentity(name, key, description);
        ValidateSpecialParameter(type, specialParameter);
        ValidateStaticPreview(type, source, preview);
        ValidateTrigger(trigger);
        ValidateSource(type, source, calc, formula, scenario);
    }

    private static void ValidateIdentity(string? name, string? key, string? description)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            throw new InvalidOperationException("Имя тега обязательно.");
        }

        if (normalizedName.Length > MaxTagNameLength)
        {
            throw new InvalidOperationException($"Имя тега не должно быть длиннее {MaxTagNameLength} символов.");
        }

        if (normalizedName.Any(char.IsControl))
        {
            throw new InvalidOperationException("Имя тега не должно содержать управляющие символы.");
        }

        var normalizedKey = key?.Trim() ?? string.Empty;
        if (normalizedKey.Length == 0)
        {
            throw new InvalidOperationException("Ключ тега обязателен.");
        }

        if (normalizedKey.Length > MaxTagKeyLength)
        {
            throw new InvalidOperationException($"Ключ тега не должен быть длиннее {MaxTagKeyLength} символов.");
        }

        if (normalizedKey.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Ключ тега не должен содержать пробельные символы.");
        }

        if (normalizedKey.Any(char.IsControl))
        {
            throw new InvalidOperationException("Ключ тега не должен содержать управляющие символы.");
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            throw new InvalidOperationException($"Описание тега не должно быть длиннее {MaxDescriptionLength} символов.");
        }
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
        TagFormulaConfigDto? formula,
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
            ValidateFormulaConfig(formula);
            ValidateGeneratorCalc(calc);
            return;
        }

        if (source is TagSource.Formula or TagSource.Script)
        {
            ValidateFormulaConfig(formula);
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

        ValidateNumericCalc(calc);
    }

    private static void ValidateFormulaConfig(TagFormulaConfigDto? formula)
    {
        if (formula is null)
        {
            throw new InvalidOperationException("Для скриптового источника нужен .csx-скрипт.");
        }

        var hasScriptId = !string.IsNullOrWhiteSpace(formula.ScriptId);
        var hasInlineScript = !string.IsNullOrWhiteSpace(formula.InlineScript);
        if (!hasScriptId && !hasInlineScript)
        {
            throw new InvalidOperationException(
                "Для скриптового источника выберите сохраненный скрипт или заполните inline-скрипт.");
        }

        if (hasScriptId && hasInlineScript)
        {
            throw new InvalidOperationException(
                "Для скриптового источника нельзя одновременно указывать сохраненный и inline-скрипт.");
        }

        if (formula.ScriptId is { Length: > MaxScriptIdLength })
        {
            throw new InvalidOperationException(
                $"Идентификатор скрипта не должен быть длиннее {MaxScriptIdLength} символов.");
        }

        if (formula.InlineScript is { Length: > MaxInlineScriptLength })
        {
            throw new InvalidOperationException(
                $"Inline-скрипт не должен быть длиннее {MaxInlineScriptLength} символов.");
        }
    }

    private static void ValidateScenario(TagType type, TagScenarioConfigDto? scenario)
    {
        if (scenario is null || scenario.Segments.Count == 0)
        {
            throw new InvalidOperationException("Сценарий должен содержать хотя бы один участок.");
        }

        if (scenario.Segments.Count > MaxScenarioSegments)
        {
            throw new InvalidOperationException($"Сценарий не должен содержать больше {MaxScenarioSegments} участков.");
        }

        if (!string.IsNullOrWhiteSpace(scenario.StartValue))
        {
            ValidateTypedValue(type, scenario.StartValue, "Начальное значение сценария");
        }

        var totalDuration = 0d;
        foreach (var segment in scenario.Segments)
        {
            ValidateScenarioSegmentBasics(segment);
            totalDuration += segment.Duration;
            if (totalDuration > MaxScenarioTotalDurationSeconds)
            {
                throw new InvalidOperationException(
                    $"Суммарная длительность сценария не должна превышать {MaxScenarioTotalDurationSeconds} секунд.");
            }

            if (IsNumeric(type))
            {
                if (segment.Calc.Type != CalcType.Static && !GeneratorCalcTypes.Contains(segment.Calc.Type))
                {
                    throw new InvalidOperationException(
                        $"Формула расчета {segment.Calc.Type} недоступна для числового сценария.");
                }

                if (segment.Calc.Type == CalcType.Static)
                {
                    ValidateTypedValue(type, segment.Calc.Start, "Статическое значение участка сценария");
                }
                else
                {
                    ValidateNumericCalc(segment.Calc);
                }

                continue;
            }

            if (segment.Calc.Type != CalcType.Static)
            {
                throw new InvalidOperationException(
                    "Сценарий для этого типа данных поддерживает только статические значения.");
            }

            ValidateTypedValue(type, segment.Calc.Start, "Статическое значение участка сценария");
        }
    }

    private static void ValidateScenarioSegmentBasics(TagScenarioSegmentDto segment)
    {
        if (string.IsNullOrWhiteSpace(segment.Id))
        {
            throw new InvalidOperationException("Участок сценария должен иметь идентификатор.");
        }

        if (segment.Label is { Length: > MaxSegmentLabelLength })
        {
            throw new InvalidOperationException(
                $"Метка участка сценария не должна быть длиннее {MaxSegmentLabelLength} символов.");
        }

        if (!double.IsFinite(segment.Duration) || segment.Duration <= 0)
        {
            throw new InvalidOperationException("Длительность участка сценария должна быть больше нуля.");
        }

        if (segment.Duration > MaxCalcDurationSeconds)
        {
            throw new InvalidOperationException(
                $"Длительность участка сценария не должна превышать {MaxCalcDurationSeconds} секунд.");
        }

        if (segment.Calc is null)
        {
            throw new InvalidOperationException("Участок сценария должен содержать формулу расчета.");
        }
    }

    private static void ValidateStaticPreview(TagType type, TagSource source, string? preview)
    {
        if (source == TagSource.Static)
        {
            ValidateTypedValue(type, preview, "Статическое значение тега");
        }
    }

    private static void ValidateTrigger(TagTriggerDto? trigger)
    {
        if (trigger is null)
        {
            throw new InvalidOperationException("Триггер вычисления тега обязателен.");
        }

        switch (trigger.Mode)
        {
            case TagTriggerMode.Once:
                if (trigger.Event is null)
                {
                    throw new InvalidOperationException("Для событийного триггера нужно выбрать событие.");
                }

                break;
            case TagTriggerMode.Interval:
                if (trigger.IntervalValue is null or <= 0)
                {
                    throw new InvalidOperationException("Интервал вычисления тега должен быть больше нуля.");
                }

                if (trigger.IntervalUnit is null)
                {
                    throw new InvalidOperationException("Для периодического триггера нужно выбрать единицу интервала.");
                }

                break;
            case TagTriggerMode.Cron:
                ValidateCron(trigger.Cron);
                break;
            default:
                throw new InvalidOperationException("Неизвестный режим триггера тега.");
        }
    }

    private static void ValidateCron(string? cron)
    {
        var normalized = NormalizeCron(cron);
        if (normalized is null || !CronExpression.IsValidExpression(normalized))
        {
            throw new InvalidOperationException("Cron-выражение тега некорректно.");
        }
    }

    private static string? NormalizeCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return null;
        }

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 5)
        {
            var dayOfMonth = parts[2];
            var dayOfWeek = parts[4];
            if (dayOfMonth == "*" && dayOfWeek == "*")
            {
                dayOfWeek = "?";
            }
            else if (dayOfMonth == "*")
            {
                dayOfMonth = "?";
            }
            else if (dayOfWeek == "*")
            {
                dayOfWeek = "?";
            }

            return $"0 {parts[0]} {parts[1]} {dayOfMonth} {parts[3]} {dayOfWeek}";
        }

        return parts.Length is 6 or 7 ? string.Join(' ', parts) : null;
    }

    private static void ValidateNumericCalc(TagCalcConfigDto calc)
    {
        ValidateDistortion(calc.Distortion);

        if (RangeCalcTypes.Contains(calc.Type))
        {
            ValidateNumericText(calc.Start, "Начальное значение формулы");
            ValidateNumericText(calc.Finish, "Конечное значение формулы");
        }

        if (DurationCalcTypes.Contains(calc.Type))
        {
            ValidateDuration(calc.Duration, "Длительность формулы");
        }

        if (WaveCalcTypes.Contains(calc.Type))
        {
            ValidateNumericText(calc.Start, "Центр периодической формулы");
            ValidateOptionalNonNegativeNumber(calc.Amplitude, "Амплитуда периодической формулы");
            ValidatePositiveNumber(calc.Period, "Период формулы");
        }

        if (calc.Type == CalcType.Curve)
        {
            ValidateCurvature(calc.Curvature);
        }

        if (calc.Type == CalcType.Sequence)
        {
            ValidateSequence(calc.Start);
        }
    }

    private static void ValidateDuration(int? duration, string fieldName)
    {
        if (duration is null or <= 0)
        {
            throw new InvalidOperationException($"{fieldName} должна быть больше нуля.");
        }

        if (duration.Value > MaxCalcDurationSeconds)
        {
            throw new InvalidOperationException($"{fieldName} не должна превышать {MaxCalcDurationSeconds} секунд.");
        }
    }

    private static void ValidateCurvature(double? curvature)
    {
        if (curvature is null)
        {
            return;
        }

        if (!double.IsFinite(curvature.Value) || curvature.Value <= 0 || curvature.Value > MaxCurvature)
        {
            throw new InvalidOperationException($"Кривизна формулы должна быть больше 0 и не больше {MaxCurvature}.");
        }
    }

    private static void ValidateDistortion(double? distortion)
    {
        if (distortion is null)
        {
            return;
        }

        if (!double.IsFinite(distortion.Value) || distortion.Value is < 0 or > 100)
        {
            throw new InvalidOperationException("Искажение (% шума) должно быть в диапазоне от 0 до 100.");
        }
    }

    private static void ValidateOptionalNonNegativeNumber(double? value, string fieldName)
    {
        if (value is null)
        {
            return;
        }

        if (!double.IsFinite(value.Value) || value.Value < 0 || Math.Abs(value.Value) > MaxNumericMagnitude)
        {
            throw new InvalidOperationException($"{fieldName} должна быть конечным неотрицательным числом.");
        }
    }

    private static void ValidatePositiveNumber(double? value, string fieldName)
    {
        if (value is null || !double.IsFinite(value.Value) || value.Value <= 0)
        {
            throw new InvalidOperationException($"{fieldName} должен быть больше нуля.");
        }

        if (value.Value > MaxCalcDurationSeconds)
        {
            throw new InvalidOperationException($"{fieldName} не должен превышать {MaxCalcDurationSeconds} секунд.");
        }
    }

    private static void ValidateTypedValue(TagType type, string? value, string fieldName)
    {
        switch (type)
        {
            case TagType.Int:
                ValidateIntegerText(value, fieldName);
                break;
            case TagType.Double:
                ValidateNumericText(value, fieldName);
                break;
            case TagType.Bool:
                ValidateBoolText(value, fieldName);
                break;
            case TagType.String:
                if (value is { Length: > MaxTextValueLength })
                {
                    throw new InvalidOperationException(
                        $"{fieldName} не должно быть длиннее {MaxTextValueLength} символов.");
                }

                break;
        }
    }

    private static void ValidateIntegerText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < int.MinValue
            || parsed > int.MaxValue
            || Math.Abs(parsed - Math.Round(parsed)) > double.Epsilon)
        {
            throw new InvalidOperationException($"{fieldName} должно быть целым числом.");
        }
    }

    private static void ValidateNumericText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || Math.Abs(parsed) > MaxNumericMagnitude)
        {
            throw new InvalidOperationException($"{fieldName} должно быть конечным числом.");
        }
    }

    private static void ValidateBoolText(string? value, string fieldName)
    {
        if (value?.Trim().ToLowerInvariant() is not ("true" or "false" or "0" or "1"))
        {
            throw new InvalidOperationException($"{fieldName} должно быть логическим значением.");
        }
    }

    private static void ValidateSequence(string? sequenceJson)
    {
        if (string.IsNullOrWhiteSpace(sequenceJson))
        {
            throw new InvalidOperationException("Последовательность должна содержать JSON-массив чисел.");
        }

        try
        {
            using var document = JsonDocument.Parse(sequenceJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Последовательность должна содержать непустой JSON-массив чисел.");
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var isValid = element.ValueKind switch
                {
                    JsonValueKind.Number when element.TryGetDouble(out var number) => IsValidNumericValue(number),
                    JsonValueKind.String => TryParseValidNumber(element.GetString(), out _),
                    _ => false,
                };

                if (!isValid)
                {
                    throw new InvalidOperationException("Последовательность должна содержать только конечные числа.");
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Последовательность должна содержать корректный JSON-массив чисел.");
        }
    }

    private static bool TryParseValidNumber(string? value, out double number)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            number = 0;
            return false;
        }

        return IsValidNumericValue(number);
    }

    private static bool IsValidNumericValue(double number) =>
        double.IsFinite(number) && Math.Abs(number) <= MaxNumericMagnitude;

    private static bool IsNumeric(TagType type) => type is TagType.Int or TagType.Double;
}
