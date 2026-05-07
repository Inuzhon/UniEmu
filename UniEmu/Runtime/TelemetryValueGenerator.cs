using System.Globalization;
using UniEmu.Data;
using UniEmu.Features.Contracts;

namespace UniEmu.Runtime;

public sealed class TelemetryValueGenerator
{
    public IReadOnlyDictionary<string, double> Generate(EmulatorEntity emulator, IReadOnlyList<EmulatorTagEntity> tags, DateTimeOffset timestamp)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            values[tag.Name] = GenerateTag(emulator, tag, timestamp);
        }

        return values;
    }

    public double GenerateTag(EmulatorEntity emulator, EmulatorTagEntity tag, DateTimeOffset timestamp)
    {
        var elapsedSec = emulator.StartedAt is null
            ? 0
            : Math.Max(0, (timestamp - emulator.StartedAt.Value).TotalSeconds);

        return GenerateTagValue(tag, elapsedSec);
    }

    private static double GenerateTagValue(EmulatorTagEntity tag, double elapsedSec)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source switch
        {
            TagSource.Generator => GenerateFromCalc(UniEmuJson.Deserialize<TagCalcConfigDto>(tag.CalcJson), elapsedSec, tag.Preview),
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

        return calc.Type switch
        {
            CalcType.Line => start + (finish - start) * Math.Clamp(elapsedSec / duration, 0, 1),
            CalcType.Random => Random.Shared.NextDouble() * (finish - start) + start,
            CalcType.Sinusoid => start + Math.Sin(elapsedSec / period * Math.Tau) * (calc.Amplitude ?? Math.Abs(finish - start)),
            CalcType.Square => start + (Math.Sin(elapsedSec / period * Math.Tau) >= 0 ? calc.Amplitude ?? Math.Abs(finish - start) : 0),
            CalcType.Sawtooth => start + (finish - start) * ((elapsedSec % period) / period),
            _ => ParsePreview(preview),
        };
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
                ContinueOnFormulaEnd.Zero => 0,
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
