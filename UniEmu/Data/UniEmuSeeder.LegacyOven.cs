using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// Содержит seed-данные индустриальной печи, перенесенной из legacy-эмулятора.
/// </summary>
public static partial class UniEmuSeeder
{
    /// <summary>
    /// Возвращает набор legacy-печей, управляемых одним CSX-тегом.
    /// </summary>
    /// <returns>Список настроек legacy-печей.</returns>
    private static IReadOnlyList<LegacyOvenSeedSpec> CreateLegacyOvenSpecs()
    {
        return
        [
            new LegacyOvenSeedSpec(
                Id: "em-legacy-oven1",
                Name: "Индустриальная печь Oven1",
                ProtocolId: 13,
                IntervalSec: 1,
                ProgramName: "O7735.nc"),
        ];
    }

    /// <summary>
    /// Создает эмулятор legacy-печи.
    /// </summary>
    /// <param name="spec">Настройки legacy-печи.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Сущность эмулятора.</returns>
    private static EmulatorEntity CreateLegacyOvenEmulator(LegacyOvenSeedSpec spec, DateTimeOffset now)
    {
        return new EmulatorEntity
        {
            Id = spec.Id,
            Name = spec.Name,
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = spec.ProtocolId,
            TargetUrl = "http://127.0.0.1:8080",
            IntervalSec = spec.IntervalSec,
            LastRun = now.AddMinutes(-10),
            NextRun = now.AddSeconds(spec.IntervalSec),
            TotalRequests = 0,
        };
    }

    /// <summary>
    /// Создает теги legacy-печи: все технологические теги статические, кроме управляющего CSX-тега.
    /// </summary>
    /// <param name="spec">Настройки legacy-печи.</param>
    /// <returns>Коллекция тегов печи.</returns>
    private static IEnumerable<EmulatorTagEntity> CreateLegacyOvenTags(LegacyOvenSeedSpec spec)
    {
        yield return CreateLegacyOvenStaticTag(spec, "count", "Count", "Count", TagType.Int, "-1", "Счетчик итераций");
        yield return CreateLegacyOvenStaticTag(spec, "temperature", "Temperature", "Temperature", TagType.Double, "20", "Температура", roundDigits: 3);
        yield return CreateLegacyOvenStaticTag(spec, "setpoint", "Setpoint", "Setpoint", TagType.Int, "150", "Уставка");
        yield return CreateLegacyOvenStaticTag(spec, "actual-parts", "ActualParts", "ActualParts", TagType.Int, "0", "Счетчик деталей", SpecialParameter.PartCounter);
        yield return CreateLegacyOvenStaticTag(spec, "power-on", "PowerOn", "PowerOn", TagType.Int, "1", "Питание");
        yield return CreateLegacyOvenStaticTag(spec, "cooling", "Cooling", "Cooling", TagType.Int, "0", "Остывание");
        yield return CreateLegacyOvenStaticTag(spec, "work-by-prog", "WorkByProg", "WorkByProg", TagType.Int, "1", "Тестирование (работа по программе)");
        yield return CreateLegacyOvenStaticTag(spec, "holding", "Holding", "Holding", TagType.Int, "1", "Выдержка");
        yield return CreateLegacyOvenStaticTag(spec, "loading-dse", "LoadingDSE", "LoadingDSE", TagType.Int, "0", "Тестирование гидроударом");
        yield return CreateLegacyOvenStaticTag(spec, "detail-complete", "DetailComplete", "DetailComplete", TagType.Int, "0", "Деталь изготовлена");
        yield return CreateLegacyOvenStaticTag(spec, "worker-id", "Worker_ID", "Worker_ID", TagType.Int, "55", "ИД работника");
        yield return CreateLegacyOvenStaticTag(spec, "worker-count", "Worker_Count", "Worker_Count", TagType.Int, "0", "Счетчик для циклической смены работника");
        yield return CreateLegacyOvenStaticTag(spec, "downtime-reason", "DowntimeReason", "DowntimeReason", TagType.Int, "0", "Код причины простоя");
        yield return CreateLegacyOvenScriptTag(spec);
        yield return CreateLegacyOvenStaticTag(spec, "prg-name", "PrgName", "PrgName", TagType.String, spec.ProgramName, "Имя УП", SpecialParameter.PrgName);
        yield return CreateLegacyOvenStaticTag(spec, "heating", "Heating", "Heating", TagType.Int, "0", "Нагревание");
        yield return CreateLegacyOvenStaticTag(spec, "overheating", "Overheating", "Overheating", TagType.Int, "0", "Перегрев");
    }

    /// <summary>
    /// Создает CSX-скрипты legacy-печи: константы, runtime helper и управляющий entrypoint.
    /// </summary>
    /// <param name="spec">Настройки legacy-печи.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция scoped-скриптов печи.</returns>
    private static IEnumerable<ScriptFileEntity> CreateLegacyOvenScripts(LegacyOvenSeedSpec spec, DateTimeOffset now)
    {
        yield return CreateEmulatorScript(
            "scr-legacy-oven-constants",
            "legacy-oven-constants.csx",
            spec.Id,
            """
            public static class LegacyOvenConstants
            {
                public const int SetpointValue = 150;
                public const int SetpointDelta = 3;
                public const int SetupDuration = 300;
                public const int NoDseDuration = 500;
                public const int ErrorSetpointDuration = 500;
                public const int DetailControlDuration = 200;
                public const int HoldingDuration = 800;
                public const int LoadingDuration = 200;
                public const double NoDseProbability = 0.25;
                public const double ErrorSetpointProbability = 0.10;
                public const double OverheatingProbability = 0.30;
                public const int PartCount = 5;
                public const int DowntimeReasonSetup = 132;
                public const int DowntimeReasonDetailControl = 50;
                public const int DowntimeReasonNoDse = 9;
                public const int DowntimeReasonErrorSetpoint = 189;
            }
            """,
            now.AddHours(-2).AddMinutes(20));

        yield return CreateEmulatorScript(
            "scr-legacy-oven-state",
            "legacy-oven-state.csx",
            spec.Id,
            """
            #load "legacy-oven-constants.csx"

            double ToNumber(object? value, double fallback)
            {
                return value switch
                {
                    null => fallback,
                    byte byteValue => byteValue,
                    short shortValue => shortValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    float floatValue => floatValue,
                    double doubleValue => doubleValue,
                    decimal decimalValue => (double)decimalValue,
                    bool boolValue => boolValue ? 1 : 0,
                    string stringValue => double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : fallback,
                    IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
                    _ => fallback,
                };
            }

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToNumber(tag?.Value, fallback)
                    : fallback;
            }

            int ReadInt(string key, int fallback) => (int)Math.Round(ReadNumber(key, fallback));

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? tag?.Value?.ToString() ?? fallback
                    : fallback;
            }

            void WriteTag(string key, object? value)
            {
                if (!UniEmu.Tags.TrySetValue(key, value))
                    throw new InvalidOperationException($"Legacy oven tag '{key}' was not found or is not static.");
            }

            int NextDuration(string stateKey, int baseDuration, double variation)
            {
                var existing = UniEmu.State.Get<int>(stateKey, 0);
                if (existing > 0)
                    return existing;

                var spread = (int)Math.Round(baseDuration * variation);
                var duration = Math.Max(1, baseDuration + Random.Shared.Next(-spread, spread + 1));
                UniEmu.State.Set(stateKey, duration);
                return duration;
            }

            bool ChanceOnce(string stateKey, double probability)
            {
                var stored = UniEmu.State.Get<int>(stateKey, -1);
                if (stored >= 0)
                    return stored == 1;

                var result = Random.Shared.NextDouble() <= probability;
                UniEmu.State.Set(stateKey, result ? 1 : 0);
                return result;
            }

            void SetPhase(string phase)
            {
                UniEmu.State.Set("phase", phase);
                UniEmu.State.Set("phaseElapsed", 0);
                UniEmu.State.Remove("phaseDuration");
                UniEmu.State.Remove("phaseChance");
                UniEmu.State.Remove("overheatChance");
            }

            int AdvancePhaseElapsed()
            {
                var elapsed = UniEmu.State.Get<int>("phaseElapsed", 0) + 1;
                UniEmu.State.Set("phaseElapsed", elapsed);
                return elapsed;
            }

            void IncrementTemperature()
            {
                var temperature = ReadNumber("Temperature", 20);
                var delta = Random.Shared.NextDouble() < 0.3
                    ? Random.Shared.Next(300, 601) / 1000d
                    : Random.Shared.Next(1, 6) / 1000d;
                WriteTag("Temperature", temperature + delta);
            }

            void DecrementTemperature()
            {
                var temperature = ReadNumber("Temperature", 20);
                var delta = Random.Shared.NextDouble() < 0.3
                    ? Random.Shared.Next(300, 601) / 1000d
                    : Random.Shared.Next(1, 6) / 1000d;
                WriteTag("Temperature", temperature - delta);
            }

            void HoldTemperature(int holdingTemperature, int delta)
            {
                var sign = Random.Shared.NextDouble() > 0.5 ? 1 : -1;
                var jitter = Random.Shared.NextDouble() < 0.3
                    ? Random.Shared.Next(400, 801) / 1000d
                    : Random.Shared.Next(100, 301) / 1000d;
                var temperature = ReadNumber("Temperature", holdingTemperature) + sign * jitter;

                if (temperature <= holdingTemperature - delta)
                    temperature = holdingTemperature - delta + Random.Shared.Next(1, 3);
                else if (temperature >= holdingTemperature + delta)
                    temperature = holdingTemperature + delta - Random.Shared.Next(1, 3);

                WriteTag("Temperature", temperature);
            }

            void CorrectAndHoldTemperature(int holdingTemperature, int delta)
            {
                var temperature = ReadNumber("Temperature", holdingTemperature);
                if (temperature < holdingTemperature - delta)
                    IncrementTemperature();
                else if (temperature > holdingTemperature + delta)
                    DecrementTemperature();
                else
                    HoldTemperature(holdingTemperature, delta);
            }

            bool CoolTo(double minTemperature)
            {
                var temperature = ReadNumber("Temperature", minTemperature);
                if (temperature <= minTemperature)
                {
                    WriteTag("Temperature", minTemperature);
                    return true;
                }

                DecrementTemperature();
                return ReadNumber("Temperature", temperature) <= minTemperature;
            }

            void SetWorker()
            {
                var workerCount = ReadInt("Worker_Count", 0) + 1;
                WriteTag("Worker_Count", workerCount);

                if (workerCount == 4)
                    WriteTag("Worker_ID", 24);
                if (workerCount == 18000)
                    WriteTag("Worker_ID", 0);
                if (workerCount == 18260)
                    WriteTag("Worker_ID", 55);
                if (workerCount == 35000)
                    WriteTag("Worker_ID", 0);
                if (workerCount == 36060)
                    WriteTag("Worker_ID", 56);
                if (workerCount == 50000)
                    WriteTag("Worker_ID", 0);
                if (workerCount >= 50600)
                    WriteTag("Worker_Count", 0);
            }

            void SetCommonFlags(
                int powerOn = 1,
                int cooling = 0,
                int workByProg = 0,
                int holding = 0,
                int loadingDse = 0,
                int detailComplete = 0,
                int heating = 0,
                int overheating = 0)
            {
                WriteTag("PowerOn", powerOn);
                WriteTag("Cooling", cooling);
                WriteTag("WorkByProg", workByProg);
                WriteTag("Holding", holding);
                WriteTag("LoadingDSE", loadingDse);
                WriteTag("DetailComplete", detailComplete);
                WriteTag("Heating", heating);
                WriteTag("Overheating", overheating);
            }
            """,
            now.AddHours(-2).AddMinutes(21));

        yield return CreateEmulatorScript(
            "scr-legacy-oven-main",
            "legacy-oven-main.csx",
            spec.Id,
            """
            #load "legacy-oven-state.csx"

            var count = ReadInt("Count", -1) + 1;
            WriteTag("Count", count);
            SetWorker();

            var phase = UniEmu.State.Get<string>("phase", "");
            if (string.IsNullOrWhiteSpace(phase))
            {
                phase = "Setup";
                SetPhase(phase);
                WriteTag("ActualParts", 0);
                WriteTag("PrgName", "O7735.nc");
            }

            var elapsed = AdvancePhaseElapsed();

            switch (phase)
            {
                case "Setup":
                {
                    SetCommonFlags(powerOn: 1);
                    WriteTag("DowntimeReason", LegacyOvenConstants.DowntimeReasonSetup);
                    WriteTag("Temperature", 20);
                    WriteTag("Setpoint", LegacyOvenConstants.SetpointValue);
                    var duration = NextDuration("phaseDuration", LegacyOvenConstants.SetupDuration, 0.15);
                    if (elapsed >= duration)
                        SetPhase("ErrorSetpoint");
                    break;
                }
                case "ErrorSetpoint":
                {
                    SetCommonFlags(powerOn: 1);
                    if (ChanceOnce("phaseChance", LegacyOvenConstants.ErrorSetpointProbability))
                    {
                        WriteTag("DowntimeReason", LegacyOvenConstants.DowntimeReasonErrorSetpoint);
                        var duration = NextDuration("phaseDuration", LegacyOvenConstants.ErrorSetpointDuration, 0.15);
                        if (elapsed >= duration)
                            SetPhase("InitialHeating");
                    }
                    else
                    {
                        WriteTag("DowntimeReason", 0);
                        SetPhase("InitialHeating");
                    }

                    break;
                }
                case "InitialHeating":
                {
                    SetCommonFlags(powerOn: 1, heating: 1);
                    WriteTag("DowntimeReason", 0);
                    IncrementTemperature();
                    if (ReadNumber("Temperature", 20) >= LegacyOvenConstants.SetpointValue)
                        SetPhase("PreHolding");
                    break;
                }
                case "PreHolding":
                {
                    SetCommonFlags(powerOn: 1, holding: 1);
                    WriteTag("DowntimeReason", 0);
                    CorrectAndHoldTemperature(LegacyOvenConstants.SetpointValue, LegacyOvenConstants.SetpointDelta);
                    WriteTag("Overheating", ReadNumber("Temperature", 20) > LegacyOvenConstants.SetpointValue + LegacyOvenConstants.SetpointDelta ? 1 : 0);
                    if (elapsed >= 10)
                        SetPhase("Loading");
                    break;
                }
                case "Loading":
                {
                    SetCommonFlags(powerOn: 1, loadingDse: 1);
                    WriteTag("DowntimeReason", 0);
                    var cooled = CoolTo(Math.Round(0.7 * LegacyOvenConstants.SetpointValue));
                    if (cooled)
                        HoldTemperature((int)Math.Round(0.7 * LegacyOvenConstants.SetpointValue), 10);
                    var duration = NextDuration("phaseDuration", LegacyOvenConstants.LoadingDuration, 0.15);
                    if (elapsed >= duration)
                        SetPhase("WorkHeating");
                    break;
                }
                case "WorkHeating":
                {
                    SetCommonFlags(powerOn: 1, workByProg: 1, heating: 1);
                    WriteTag("DowntimeReason", 0);
                    IncrementTemperature();
                    if (ReadNumber("Temperature", 20) >= LegacyOvenConstants.SetpointValue)
                        SetPhase("Holding");
                    break;
                }
                case "Holding":
                {
                    SetCommonFlags(powerOn: 1, workByProg: 1, holding: 1);
                    WriteTag("DowntimeReason", 0);
                    var overheating = ChanceOnce("overheatChance", LegacyOvenConstants.OverheatingProbability);
                    if (overheating && elapsed % 97 < 12)
                    {
                        IncrementTemperature();
                        WriteTag("Overheating", ReadNumber("Temperature", 20) > LegacyOvenConstants.SetpointValue + LegacyOvenConstants.SetpointDelta ? 1 : 0);
                    }
                    else
                    {
                        CorrectAndHoldTemperature(LegacyOvenConstants.SetpointValue, LegacyOvenConstants.SetpointDelta);
                        WriteTag("Overheating", 0);
                    }

                    var duration = NextDuration("phaseDuration", LegacyOvenConstants.HoldingDuration, 0.15);
                    if (elapsed >= duration)
                        SetPhase("DetailComplete");
                    break;
                }
                case "DetailComplete":
                {
                    SetCommonFlags(powerOn: 1, workByProg: 1, detailComplete: 1);
                    WriteTag("ActualParts", ReadInt("ActualParts", 0) + 1);
                    SetPhase(ReadInt("ActualParts", 0) < LegacyOvenConstants.PartCount ? "Loading" : "FinalLoading");
                    break;
                }
                case "FinalLoading":
                {
                    SetCommonFlags(powerOn: 1, cooling: 1, loadingDse: 1);
                    WriteTag("DowntimeReason", 0);
                    CoolTo(20);
                    var duration = NextDuration("phaseDuration", LegacyOvenConstants.LoadingDuration, 0.15);
                    if (elapsed >= duration)
                        SetPhase("DetailControl");
                    break;
                }
                case "DetailControl":
                {
                    SetCommonFlags(powerOn: 1, cooling: 1);
                    WriteTag("DowntimeReason", LegacyOvenConstants.DowntimeReasonDetailControl);
                    var cooled = CoolTo(20);
                    var duration = NextDuration("phaseDuration", LegacyOvenConstants.DetailControlDuration, 0.15);
                    if (cooled && elapsed >= duration)
                    {
                        WriteTag("DowntimeReason", 0);
                        SetPhase("NoDse");
                    }
                    break;
                }
                case "NoDse":
                {
                    SetCommonFlags(powerOn: 1);
                    if (ChanceOnce("phaseChance", LegacyOvenConstants.NoDseProbability))
                    {
                        WriteTag("DowntimeReason", LegacyOvenConstants.DowntimeReasonNoDse);
                        var duration = NextDuration("phaseDuration", LegacyOvenConstants.NoDseDuration, 0.15);
                        if (elapsed >= duration)
                            SetPhase("SimpleDowntime");
                    }
                    else
                    {
                        WriteTag("DowntimeReason", 0);
                        SetPhase("SimpleDowntime");
                    }

                    break;
                }
                default:
                {
                    SetCommonFlags(powerOn: 1);
                    WriteTag("DowntimeReason", 0);
                    WriteTag("ActualParts", 0);
                    SetPhase("Setup");
                    break;
                }
            }

            return count;
            """,
            now.AddHours(-2).AddMinutes(22));
    }

    /// <summary>
    /// Создает статический тег legacy-печи.
    /// </summary>
    private static EmulatorTagEntity CreateLegacyOvenStaticTag(
        LegacyOvenSeedSpec spec,
        string idSuffix,
        string name,
        string key,
        TagType type,
        string preview,
        string description,
        SpecialParameter specialParameter = SpecialParameter.None,
        int? roundDigits = null)
    {
        return CreateTag(
            spec.Id,
            $"legacy-oven-{idSuffix}",
            name,
            key,
            type,
            TagSource.Static,
            preview,
            roundDigits: roundDigits,
            specialParameter: specialParameter,
            description: description);
    }

    /// <summary>
    /// Создает непубликуемый управляющий CSX-тег legacy-печи.
    /// </summary>
    private static EmulatorTagEntity CreateLegacyOvenScriptTag(LegacyOvenSeedSpec spec)
    {
        var tag = CreateTag(
            spec.Id,
            "legacy-oven-script",
            "Script",
            "Script",
            TagType.Int,
            TagSource.Script,
            "0",
            formula: ScriptFormula("scr-legacy-oven-main"),
            description: "Управляющий CSX-скрипт legacy-печи. Обновляет остальные static-теги и не отправляется в Dispatcher.");
        tag.Enabled = false;
        return tag;
    }

    /// <summary>
    /// Настройки legacy-печи.
    /// </summary>
    private sealed record LegacyOvenSeedSpec(
        string Id,
        string Name,
        int ProtocolId,
        int IntervalSec,
        string ProgramName);
}
