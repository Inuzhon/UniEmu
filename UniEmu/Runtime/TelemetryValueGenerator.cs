using System.Globalization;
using System.Text.Json;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Runtime;

/// <summary>
/// Результат расчета значения тега перед отправкой в Dispatcher и realtime-каналы.
/// </summary>
/// <param name="Key">Ключ тега во внешнем протоколе.</param>
/// <param name="Name">Отображаемое имя тега.</param>
/// <param name="Value">Типизированное значение тега.</param>
/// <param name="NumericValue">Числовое представление значения, если оно доступно.</param>
/// <param name="SpecialParameter">Специальный CNC-параметр, связанный с тегом.</param>
public sealed record GeneratedTagValue(
    string Key,
    string Name,
    object? Value,
    double? NumericValue,
    SpecialParameter? SpecialParameter);

/// <summary>
/// Генерирует значения тегов из статических preview-значений, формул и сценариев.
/// </summary>
public sealed class TelemetryValueGenerator
{
    /// <summary>
    /// Генерирует только числовые значения тегов для legacy-пакета телеметрии.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого выполняется расчет.</param>
    /// <param name="tags">Список тегов эмулятора.</param>
    /// <param name="timestamp">Время расчета.</param>
    /// <returns>Словарь значений по ключам тегов.</returns>
    public IReadOnlyDictionary<string, double> Generate(EmulatorEntity emulator, IReadOnlyList<EmulatorTagEntity> tags, DateTimeOffset timestamp)
    {
        return GenerateTagValues(emulator, tags, timestamp)
            .Where(value => value.NumericValue is not null)
            .ToDictionary(value => value.Key, value => value.NumericValue!.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Генерирует типизированные значения всех переданных тегов.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого выполняется расчет.</param>
    /// <param name="tags">Список тегов эмулятора.</param>
    /// <param name="timestamp">Время расчета.</param>
    /// <returns>Список рассчитанных значений тегов.</returns>
    public IReadOnlyList<GeneratedTagValue> GenerateTagValues(EmulatorEntity emulator, IReadOnlyList<EmulatorTagEntity> tags, DateTimeOffset timestamp)
    {
        return tags
            .Select(tag => GenerateTag(emulator, tag, timestamp))
            .ToList();
    }

    /// <summary>
    /// Генерирует типизированное значение одного тега.
    /// </summary>
    /// <param name="emulator">Эмулятор, которому принадлежит тег.</param>
    /// <param name="tag">Тег для расчета.</param>
    /// <param name="timestamp">Время расчета.</param>
    /// <returns>Результат расчета тега.</returns>
    public GeneratedTagValue GenerateTag(EmulatorEntity emulator, EmulatorTagEntity tag, DateTimeOffset timestamp)
    {
        var numericValue = GenerateNumericTag(emulator, tag, timestamp);
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = ApplyTagRounding(tagType, tag, CastValue(tagType, tag, numericValue));
        SpecialParameter? specialParameter = string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);

        return new GeneratedTagValue(tag.Key, tag.Name, value, ToNumericValue(value), specialParameter);
    }

    private static double GenerateNumericTag(EmulatorEntity emulator, EmulatorTagEntity tag, DateTimeOffset timestamp)
    {
        var elapsedSec = emulator.StartedAt is null
            ? 0
            : Math.Max(0, (timestamp - emulator.StartedAt.Value).TotalSeconds);

        return GenerateTagValue(tag, elapsedSec);
    }

    private static object? CastValue(TagType tagType, EmulatorTagEntity tag, double numericValue)
    {
        return tagType switch
        {
            TagType.Bool => numericValue != 0,
            TagType.Int => (int)Math.Round(numericValue),
            TagType.Double => numericValue,
            TagType.String => GetStringValue(tag, numericValue),
            _ => null,
        };
    }

    private static string GetStringValue(EmulatorTagEntity tag, double numericValue)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source is TagSource.Static or TagSource.Script or TagSource.Cnc)
        {
            return tag.Preview;
        }

        return numericValue.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Преобразует поддерживаемое типизированное значение тега в число.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <returns>Числовое представление или <see langword="null"/> для нечислового значения.</returns>
    public static double? ToNumericValue(object? value)
    {
        return value switch
        {
            bool boolValue => boolValue ? 1 : 0,
            int intValue => intValue,
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            long longValue => longValue,
            decimal decimalValue => (double)decimalValue,
            _ => null,
        };
    }

    /// <summary>
    /// Применяет округление к double-тегу по настройке тега.
    /// </summary>
    /// <param name="tagType">Тип значения тега.</param>
    /// <param name="tag">Настройки тега.</param>
    /// <param name="value">Исходное значение.</param>
    /// <returns>Округленное значение или исходное значение, если округление не применяется.</returns>
    public static object? ApplyTagRounding(TagType tagType, EmulatorTagEntity tag, object? value)
    {
        if (tagType != TagType.Double || tag.RoundDigits is null || value is null)
        {
            return value;
        }

        var digits = Math.Clamp(tag.RoundDigits.Value, 0, 15);
        var numericValue = ToNumericValue(value);
        return numericValue is null ? value : Math.Round(numericValue.Value, digits, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Преобразует значение тега в строку preview для хранения и отображения.
    /// </summary>
    /// <param name="value">Типизированное значение.</param>
    /// <returns>Строковое представление в инвариантной культуре.</returns>
    public static string ToPreview(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// Восстанавливает типизированное значение тега из preview-строки.
    /// </summary>
    /// <param name="tagType">Ожидаемый тип тега.</param>
    /// <param name="preview">Строковое preview-значение.</param>
    /// <returns>Типизированное значение тега.</returns>
    public static object? FromPreview(TagType tagType, string preview)
    {
        return tagType switch
        {
            TagType.Bool => ParsePreview(preview) != 0,
            TagType.Int => (int)Math.Round(ParsePreview(preview)),
            TagType.Double => ParsePreview(preview),
            TagType.String => preview,
            _ => preview,
        };
    }

    private static double GenerateTagValue(EmulatorTagEntity tag, double elapsedSec)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source switch
        {
            TagSource.Generator or TagSource.FormulaScript => GenerateFromCalc(UniEmuJson.Deserialize<TagCalcConfigDto>(tag.CalcJson), elapsedSec, tag.Preview),
            TagSource.Scenario => GenerateFromScenario(UniEmuJson.Deserialize<TagScenarioConfigDto>(tag.ScenarioJson), elapsedSec, tag.Preview),
            _ => ParsePreview(tag.Preview),
        };
    }

    private static double GenerateFromCalc(TagCalcConfigDto? calc, double elapsedSec, string preview)
    {
        if (calc is null)
        {
            return ParsePreview(preview);
        }

        var start = ParsePreview(calc.Start ?? preview);
        var finish = ParsePreview(calc.Finish ?? preview);
        var duration = Math.Max(1, calc.Duration ?? 60);
        var period = Math.Max(1, calc.Period ?? duration);
        var boundedElapsedSec = GetCyclicElapsed(elapsedSec, duration);
        var progress = Math.Clamp(boundedElapsedSec / duration, 0, 1);
        var phase = (elapsedSec % period) / period;
        var amplitude = calc.Amplitude ?? Math.Abs(finish - start);

        var value = calc.Type switch
        {
            CalcType.Line => start + (finish - start) * progress,
            CalcType.Curve => start + (finish - start) * Math.Pow(progress, calc.Curvature ?? 2),
            CalcType.Sequence => GenerateFromSequence(calc.Start, progress, preview),
            CalcType.Random => GenerateRandom(start, finish),
            CalcType.Sinusoid => start + Math.Sin(elapsedSec / period * Math.Tau) * amplitude,
            CalcType.Square => start + (phase < 0.5 ? amplitude : -amplitude),
            CalcType.Sawtooth => start + amplitude * (2 * phase - 1),
            CalcType.SquircleEarly => start + (finish - start) * (1 - Math.Pow(1 - progress, 2)),
            CalcType.SquircleLate => start + (finish - start) * Math.Pow(progress, 2),
            _ => ParsePreview(preview),
        };

        return ApplyDistortion(value, calc.Distortion, boundedElapsedSec, progress);
    }

    private static double GetCyclicElapsed(double elapsedSec, double duration)
    {
        if (elapsedSec <= duration)
        {
            return Math.Max(0, elapsedSec);
        }

        var remainder = elapsedSec % duration;
        return remainder == 0 ? duration : remainder;
    }

    private static double ApplyDistortion(double value, double? distortion, double elapsedSec, double progress)
    {
        if (distortion is null or <= 0)
        {
            return value;
        }

        var distortionRatio = distortion.Value / 100;
        var noise = Math.Sin(elapsedSec * 53.13 + progress * 17.7) * 0.5 * distortionRatio;
        var scale = Math.Max(1, Math.Abs(value));
        return value + noise * scale;
    }

    private static double GenerateRandom(double start, double finish)
    {
        var min = Math.Min(start, finish);
        var max = Math.Max(start, finish);
        return Random.Shared.NextDouble() * (max - min) + min;
    }

    private static double GenerateFromSequence(string? sequenceJson, double progress, string preview)
    {
        if (string.IsNullOrWhiteSpace(sequenceJson))
        {
            return ParsePreview(preview);
        }

        try
        {
            using var document = JsonDocument.Parse(sequenceJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return ParsePreview(preview);
            }

            var index = Math.Min(document.RootElement.GetArrayLength() - 1, (int)Math.Floor(progress * document.RootElement.GetArrayLength()));
            var element = document.RootElement[index];
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetDouble(out var value) => value,
                JsonValueKind.String => ParsePreview(element.GetString()),
                _ => ParsePreview(preview),
            };
        }
        catch (JsonException)
        {
            return ParsePreview(preview);
        }
    }

    private static double GenerateFromScenario(TagScenarioConfigDto? scenario, double elapsedSec, string preview)
    {
        if (scenario is null || scenario.Segments.Count == 0)
        {
            return ParsePreview(preview);
        }

        var total = scenario.Segments.Sum(s => Math.Max(0, s.Duration));
        if (total <= 0)
        {
            return ParsePreview(preview);
        }

        var position = elapsedSec;
        if (position > total)
        {
            position = scenario.ContinueOnFormulaEnd switch
            {
                ContinueOnFormulaEnd.Repeat => position % total,
                ContinueOnFormulaEnd.Zero => double.NaN,
                ContinueOnFormulaEnd.Stretch => total,
                _ => double.NaN,
            };
        }

        if (double.IsNaN(position))
        {
            return 0;
        }

        var offset = 0d;
        foreach (var segment in scenario.Segments)
        {
            var duration = Math.Max(0, segment.Duration);
            if (position <= offset + duration)
            {
                return GenerateFromCalc(segment.Calc, Math.Max(0, position - offset), preview);
            }

            offset += duration;
        }

        return GenerateFromCalc(scenario.Segments[^1].Calc, scenario.Segments[^1].Duration, preview);
    }

    private static double ParsePreview(string? value)
    {
        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue ? 1 : 0;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}
