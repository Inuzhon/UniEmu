using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// Содержит seed-данные ЧПУ-станков, CNC-программ и связанных скриптов.
/// </summary>
public static partial class UniEmuSeeder
{
    /// <summary>
    /// Возвращает набор демонстрационных ЧПУ-станков разных типов.
    /// </summary>
    /// <returns>Список настроек ЧПУ-станков для начального заполнения.</returns>
    private static IReadOnlyList<CncSeedSpec> CreateCncSpecs()
    {
        return
        [
            new CncSeedSpec(
                Id: "em-cnc-vmc-650-01",
                Name: "CNC_VMC_650_01",
                ProtocolId: 41,
                IntervalSec: 1,
                TotalRequests: 45120,
                ProgramName: "VMC650_MAIN.NC",
                Model: "Fanuc 0i-MF Plus",
                FirmwareVersion: "31i-B5 6.17",
                SerialNumber: "VMC650-2026-0142",
                PlcVersion: "PMC-Ladder 4.08",
                ActiveTool: 7,
                CommandFeed: 780,
                FeedOverrideBase: 95,
                RapidOverrideBase: 50,
                SpindleCommand: 8200,
                SpindleActualBase: 8150,
                SpindleAmplitude: 55,
                SpindleLoadBase: 42,
                SpindleLoadAmplitude: 9,
                SpindleTemperatureBase: 47,
                AxisLoadBase: 24,
                VibrationBase: 2.1,
                XStart: -120,
                XFinish: 380,
                YStart: -80,
                YFinish: 260,
                ZStart: 180,
                ZFinish: -45,
                DistanceMax: 180),
            new CncSeedSpec(
                Id: "em-cnc-lathe-turn-200-02",
                Name: "CNC_Lathe_Turn_200_02",
                ProtocolId: 42,
                IntervalSec: 1,
                TotalRequests: 38760,
                ProgramName: "LATHE200_MAIN.NC",
                Model: "Siemens SINUMERIK 828D",
                FirmwareVersion: "PPU.4.95 SP3",
                SerialNumber: "TURN200-2026-0098",
                PlcVersion: "S7-200 3.12",
                ActiveTool: 3,
                CommandFeed: 420,
                FeedOverrideBase: 90,
                RapidOverrideBase: 25,
                SpindleCommand: 3200,
                SpindleActualBase: 3180,
                SpindleAmplitude: 38,
                SpindleLoadBase: 55,
                SpindleLoadAmplitude: 12,
                SpindleTemperatureBase: 52,
                AxisLoadBase: 31,
                VibrationBase: 2.8,
                XStart: 210,
                XFinish: 38,
                YStart: 0,
                YFinish: 0,
                ZStart: 420,
                ZFinish: -160,
                DistanceMax: 240),
            new CncSeedSpec(
                Id: "em-cnc-router-gantry-03",
                Name: "CNC_Router_Gantry_03",
                ProtocolId: 43,
                IntervalSec: 2,
                TotalRequests: 26440,
                ProgramName: "ROUTER03_NESTING.NC",
                Model: "Syntec 6MB-E",
                FirmwareVersion: "10.118.52",
                SerialNumber: "ROUTER03-2026-0217",
                PlcVersion: "Ladder 2.7.4",
                ActiveTool: 12,
                CommandFeed: 5600,
                FeedOverrideBase: 100,
                RapidOverrideBase: 75,
                SpindleCommand: 18000,
                SpindleActualBase: 17940,
                SpindleAmplitude: 120,
                SpindleLoadBase: 36,
                SpindleLoadAmplitude: 7,
                SpindleTemperatureBase: 44,
                AxisLoadBase: 22,
                VibrationBase: 1.7,
                XStart: 0,
                XFinish: 1450,
                YStart: 0,
                YFinish: 900,
                ZStart: 120,
                ZFinish: -12,
                DistanceMax: 560),
        ];
    }

    /// <summary>
    /// Создает строку эмулятора ЧПУ-станка с базовой runtime-статистикой.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Сущность эмулятора ЧПУ-станка.</returns>
    private static EmulatorEntity CreateCncEmulator(CncSeedSpec spec, DateTimeOffset now)
    {
        return new EmulatorEntity
        {
            Id = spec.Id,
            Name = spec.Name,
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = spec.ProtocolId,
            TargetUrl = "https://scada.local/api/cnc/ingest",
            IntervalSec = spec.IntervalSec,
            LastRun = now.AddMinutes(-7),
            NextRun = now.AddSeconds(spec.IntervalSec),
            TotalRequests = spec.TotalRequests,
        };
    }

    /// <summary>
    /// Создает полный набор тегов ЧПУ-станка: состояния, оси, шпиндель, подачу, инструмент, диагностику и паспортные данные.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <returns>Теги для указанного ЧПУ-станка.</returns>
    private static IEnumerable<EmulatorTagEntity> CreateCncTags(CncSeedSpec spec)
    {
        yield return CreateTag(spec, "power-state", "PowerState", "PowerState", TagType.Bool, TagSource.Scenario, "true",
            scenario: CreatePowerScenario(), description: "Состояние питания стойки УЧПУ.");
        yield return CreateTag(spec, "controller-mode", "ControllerMode", "ControllerMode", TagType.String, TagSource.Scenario, "AUTO",
            scenario: CreateControllerModeScenario(), specialParameter: SpecialParameter.WorkMode, description: "Режим стойки: AUTO, MDI, JOG или EDIT.");
        yield return CreateTag(spec, "execution-state", "ExecutionState", "ExecutionState", TagType.String, TagSource.Scenario, "READY",
            scenario: CreateExecutionStateScenario(), specialParameter: SpecialParameter.SystemState, description: "MTConnect-подобное состояние выполнения программы.");
        yield return CreateTag(spec, "cycle-state", "CycleState", "CycleState", TagType.String, TagSource.Scenario, "Reset",
            scenario: CreateCycleStateScenario(), description: "Состояние кнопок цикла: Reset, Cycle Start или Feed Hold.");
        yield return CreateTag(spec, "program-name", "ProgramName", "ProgramName", TagType.String, TagSource.Static, spec.ProgramName,
            specialParameter: SpecialParameter.PrgName, description: "Имя основной УП, по которому publish-задача выбирает CNC-программу.");
        yield return CreateTag(spec, "subprogram-name", "SubprogramName", "SubprogramName", TagType.String, TagSource.Static, string.Empty,
            specialParameter: SpecialParameter.Subprogram, description: "Имя активной подпрограммы, если она выбрана оператором.");
        yield return CreateTag(spec, "frame-number", "FrameNumber", "FrameNumber", TagType.Int, TagSource.Static, "0",
            specialParameter: SpecialParameter.FrameNum, description: "Номер текущего кадра, рассчитываемый publish-задачей по выбранной УП.");
        yield return CreateTag(spec, "frame-text", "FrameText", "FrameText", TagType.String, TagSource.Static, string.Empty,
            specialParameter: SpecialParameter.FrameText, description: "Текст текущего кадра, рассчитываемый publish-задачей по выбранной УП.");
        yield return CreateTag(spec, "door-closed", "DoorClosed", "DoorClosed", TagType.Bool, TagSource.Scenario, "true",
            scenario: CreateDoorClosedScenario(), description: "Состояние защитных дверей рабочей зоны.");
        yield return CreateTag(spec, "fixture-clamped", "FixtureClamped", "FixtureClamped", TagType.Bool, TagSource.Scenario, "true",
            scenario: CreateFixtureClampedScenario(), description: "Состояние зажима детали или патрона.");
        yield return CreateTag(spec, "servo-ready", "ServoReady", "ServoReady", TagType.Bool, TagSource.Scenario, "true",
            scenario: CreateServoReadyScenario(), description: "Готовность сервоприводов осей.");
        yield return CreateTag(spec, "machine-readiness", "MachineReadiness", "MachineReadiness", TagType.Bool, TagSource.Script, "true",
            formula: ScriptFormula("scr-cnc-machine-readiness"), specialParameter: SpecialParameter.MachineReadiness, description: "Интегральная готовность станка по питанию, дверям, зажиму и сервоприводам.");
        yield return CreateTag(spec, "technological-stop", "TechnologicalStop", "TechnologicalStop", TagType.Bool, TagSource.Script, "false",
            formula: ScriptFormula("scr-cnc-tech-stop"), specialParameter: SpecialParameter.TechnologicalStop, description: "Технологический останов, связанный с HOLD или ожиданием оператора.");
        yield return CreateTag(spec, "emergency-stop", "EmergencyStop", "EmergencyStop", TagType.Bool, TagSource.Script, "false",
            formula: ScriptFormula("scr-cnc-emergency-stop"), specialParameter: SpecialParameter.EmergencyStop, description: "Аварийный останов, рассчитанный по состоянию ALARM и диагностическому коду.");
        yield return CreateTag(spec, "machine-x", "MachineX", "MachineX", TagType.Double, TagSource.Scenario, Invariant(spec.XStart),
            scenario: CreateAxisScenario("x", spec.XStart, spec.XFinish), roundDigits: 3, specialParameter: SpecialParameter.AxisPosition, description: "Фактическая машинная позиция оси X.");
        yield return CreateTag(spec, "machine-y", "MachineY", "MachineY", TagType.Double, TagSource.Scenario, Invariant(spec.YStart),
            scenario: CreateAxisScenario("y", spec.YStart, spec.YFinish), roundDigits: 3, description: "Фактическая машинная позиция оси Y.");
        yield return CreateTag(spec, "machine-z", "MachineZ", "MachineZ", TagType.Double, TagSource.Scenario, Invariant(spec.ZStart),
            scenario: CreateAxisScenario("z", spec.ZStart, spec.ZFinish), roundDigits: 3, description: "Фактическая машинная позиция оси Z.");
        yield return CreateTag(spec, "distance-to-go", "DistanceToGo", "DistanceToGo", TagType.Double, TagSource.Scenario, Invariant(spec.DistanceMax),
            scenario: CreateDistanceToGoScenario(spec), roundDigits: 3, description: "Остаток перемещения до конца активного кадра.");
        yield return CreateTag(spec, "spindle-command", "SpindleCommandRpm", "SpindleCommandRpm", TagType.Double, TagSource.Scenario, Invariant(spec.SpindleCommand),
            scenario: CreateSpindleCommandScenario(spec), roundDigits: 0, description: "Командная скорость шпинделя S из управляющей программы.");
        yield return CreateTag(spec, "spindle-actual", "SpindleActualRpm", "SpindleActualRpm", TagType.Double, TagSource.Generator, Invariant(spec.SpindleActualBase),
            calc: SinusoidCalc(spec.SpindleActualBase, spec.SpindleAmplitude, 24, distortion: 0.5), roundDigits: 0, specialParameter: SpecialParameter.SpindleSpeed, description: "Фактическая скорость шпинделя с небольшой волной регулирования.");
        yield return CreateTag(spec, "spindle-direction", "SpindleDirection", "SpindleDirection", TagType.String, TagSource.Scenario, "CW",
            scenario: CreateSpindleDirectionScenario(), description: "Направление вращения шпинделя: CW, CCW или STOPPED.");
        yield return CreateTag(spec, "spindle-load", "SpindleLoadPct", "SpindleLoadPct", TagType.Double, TagSource.Generator, Invariant(spec.SpindleLoadBase),
            calc: SinusoidCalc(spec.SpindleLoadBase, spec.SpindleLoadAmplitude, 35, distortion: 1.2), roundDigits: 1, specialParameter: SpecialParameter.SpindleLoad, description: "Нагрузка шпинделя в процентах.");
        yield return CreateTag(spec, "vibration", "VibrationMmS", "VibrationMmS", TagType.Double, TagSource.Generator, Invariant(spec.VibrationBase),
            calc: SinusoidCalc(spec.VibrationBase, 0.35, 18, distortion: 2.0), roundDigits: 2, description: "Вибрация шпиндельного узла в мм/с.");
        yield return CreateTag(spec, "spindle-temperature", "SpindleTemperatureC", "SpindleTemperatureC", TagType.Double, TagSource.Generator, Invariant(spec.SpindleTemperatureBase),
            calc: SinusoidCalc(spec.SpindleTemperatureBase, 3.5, 180, distortion: 0.8), roundDigits: 1, description: "Температура шпиндельного узла.");
        yield return CreateTag(spec, "spindle-override", "SpindleOverridePct", "SpindleOverridePct", TagType.Double, TagSource.Generator, "100",
            calc: SequenceCalc(80, 100, 100, 120, 100), roundDigits: 0, specialParameter: SpecialParameter.SpindleOvr, description: "Операторская коррекция оборотов шпинделя.");
        yield return CreateTag(spec, "command-feed", "CommandFeedMmMin", "CommandFeedMmMin", TagType.Double, TagSource.Scenario, Invariant(spec.CommandFeed),
            scenario: CreateCommandFeedScenario(spec), roundDigits: 1, description: "Командная подача из УП.");
        yield return CreateTag(spec, "actual-feed", "ActualFeedMmMin", "ActualFeedMmMin", TagType.Double, TagSource.Script, "0",
            formula: ScriptFormula("scr-cnc-actual-feed"), roundDigits: 1, specialParameter: SpecialParameter.FeedRate, description: "Фактическая подача с учетом режима выполнения и override.");
        yield return CreateTag(spec, "feed-override", "FeedOverridePct", "FeedOverridePct", TagType.Double, TagSource.Generator, Invariant(spec.FeedOverrideBase),
            calc: SequenceCalc(spec.FeedOverrideBase, 100, 100, 80, spec.FeedOverrideBase), roundDigits: 0, specialParameter: SpecialParameter.FeedOvr, description: "Коррекция рабочей подачи оператором.");
        yield return CreateTag(spec, "rapid-override", "RapidOverridePct", "RapidOverridePct", TagType.Double, TagSource.Generator, Invariant(spec.RapidOverrideBase),
            calc: SequenceCalc(25, spec.RapidOverrideBase, 50, 100, spec.RapidOverrideBase), roundDigits: 0, description: "Коррекция быстрых перемещений G00.");
        yield return CreateTag(spec, "active-motion-mode", "ActiveMotionMode", "ActiveMotionMode", TagType.String, TagSource.Scenario, "G00",
            scenario: CreateMotionModeScenario(), description: "Активный режим интерполяции: G00, G01, G02 или G03.");
        yield return CreateTag(spec, "active-tool", "ActiveTool", "ActiveTool", TagType.Int, TagSource.Scenario, Invariant(spec.ActiveTool),
            scenario: CreateActiveToolScenario(spec), specialParameter: SpecialParameter.ToolNum, description: "Номер активного инструмента или позиции револьвера.");
        yield return CreateTag(spec, "tool-life", "ToolLifeRemainingPct", "ToolLifeRemainingPct", TagType.Double, TagSource.Generator, "100",
            calc: new TagCalcConfigDto(CalcType.Line, "100", "8", 900, null, null, null, 0.4), roundDigits: 1, description: "Остаток ресурса активного инструмента.");
        yield return CreateTag(spec, "axis-load", "AxisLoadPct", "AxisLoadPct", TagType.Double, TagSource.FormulaScript, Invariant(spec.AxisLoadBase),
            calc: SinusoidCalc(spec.AxisLoadBase, 5, 28, distortion: 1.0), formula: ScriptFormula("scr-cnc-axis-load"), roundDigits: 1, specialParameter: SpecialParameter.AxisLoad, description: "Нагрузка оси после генератора и CSX-постобработки по подаче и шпинделю.");
        yield return CreateTag(spec, "cycle-time", "CycleTimeSec", "CycleTimeSec", TagType.Double, TagSource.Formula, "0",
            formula: ScriptFormula("scr-cnc-cycle-time"), roundDigits: 1, specialParameter: SpecialParameter.CycleTime, description: "Накопленное время активного цикла с persistent state скрипта.");
        yield return CreateTag(spec, "alarm-code", "AlarmCode", "AlarmCode", TagType.Int, TagSource.Script, "0",
            formula: ScriptFormula("scr-cnc-alarm-code"), specialParameter: SpecialParameter.ErrorNum, description: "Код аварии по дверям, зажиму, сервоприводам, нагрузке и вибрации.");
        yield return CreateTag(spec, "alarm-text", "AlarmText", "AlarmText", TagType.String, TagSource.Script, string.Empty,
            formula: ScriptFormula("scr-cnc-alarm-text"), specialParameter: SpecialParameter.ErrorText, description: "Текст аварии, соответствующий AlarmCode.");
        yield return CreateTag(spec, "warning-text", "WarningText", "WarningText", TagType.String, TagSource.Script, string.Empty,
            formula: ScriptFormula("scr-cnc-warning-text"), specialParameter: SpecialParameter.Message, description: "Предупреждение о ресурсе инструмента, температуре или вибрации.");
        yield return CreateTag(spec, "cnc-model", "CncModel", "CncModel", TagType.String, TagSource.Cnc, spec.Model,
            specialParameter: SpecialParameter.CNCModel, description: "Модель стойки УЧПУ.");
        yield return CreateTag(spec, "firmware-version", "FirmwareVersion", "FirmwareVersion", TagType.String, TagSource.Cnc, spec.FirmwareVersion,
            specialParameter: SpecialParameter.FirmwareVersion, description: "Версия прошивки стойки.");
        yield return CreateTag(spec, "serial-number", "SerialNumber", "SerialNumber", TagType.String, TagSource.Cnc, spec.SerialNumber,
            specialParameter: SpecialParameter.SerialNumber, description: "Серийный номер стойки или станка.");
        yield return CreateTag(spec, "plc-version", "PlcVersion", "PlcVersion", TagType.String, TagSource.Cnc, spec.PlcVersion,
            specialParameter: SpecialParameter.PLCVersion, description: "Версия PLC-проекта стойки.");
    }

    /// <summary>
    /// Создает CNC-программы, привязанные к seed-станкам.
    /// </summary>
    /// <param name="specs">Настройки демонстрационных ЧПУ-станков.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция CNC-программ.</returns>
    private static IEnumerable<CncProgramEntity> CreateCncPrograms(IEnumerable<CncSeedSpec> specs, DateTimeOffset now)
    {
        foreach (var spec in specs)
        {
            yield return CreateCncProgram(
                $"cnc-{spec.Id["em-cnc-".Length..]}-main",
                spec.ProgramName,
                spec.Id,
                $"Seed: основная управляющая программа для {spec.Name}.",
                CreateCncProgramContent(spec.ProgramName),
                now.AddHours(-1));
        }
    }

    /// <summary>
    /// Создает shared CSX-скрипты, используемые тегами ЧПУ-станков.
    /// </summary>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция shared-скриптов.</returns>
    private static IEnumerable<ScriptFileEntity> CreateSharedCncScripts(DateTimeOffset now)
    {
        yield return CreateSharedScript("scr-cnc-math", "cnc/math.csx",
            """
            double Clamp(double value, double min, double max)
            {
                if (value < min)
                    return min;

                if (value > max)
                    return max;

                return value;
            }

            double ToDouble(object? value, double fallback)
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

            string ToText(object? value, string fallback)
            {
                return value?.ToString() ?? fallback;
            }

            bool ToBool(object? value, bool fallback)
            {
                return value switch
                {
                    null => fallback,
                    bool boolValue => boolValue,
                    string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                    _ => ToDouble(value, fallback ? 1 : 0) != 0,
                };
            }
            """,
            now.AddHours(-1).AddMinutes(1));

        yield return CreateSharedScript("scr-cnc-actual-feed", "cnc/actual-feed.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            var execution = ReadText("ExecutionState", "READY");
            if (execution != "ACTIVE")
                return 0d;

            var commandFeed = ReadNumber("CommandFeedMmMin", 0);
            var feedOverride = ReadNumber("FeedOverridePct", 100);
            var rapidOverride = ReadNumber("RapidOverridePct", 100);
            var motion = ReadText("ActiveMotionMode", "G01");
            var multiplier = motion == "G00"
                ? rapidOverride
                : feedOverride;

            return Math.Round(Clamp(commandFeed * multiplier / 100d, 0, 20000), 1);
            """,
            now.AddHours(-1).AddMinutes(2));

        yield return CreateSharedScript("scr-cnc-cycle-time", "cnc/cycle-time.csx",
            """
            #load "math.csx"

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            var execution = ReadText("ExecutionState", "READY");
            var cycleSeconds = UniEmu.State.Get<double>("cycleSeconds", 0);

            if (execution == "ACTIVE")
            {
                var previous = UniEmu.State.PrevTimestamp;
                if (previous is not null)
                    cycleSeconds += Math.Max(0, (Now - previous.Value).TotalSeconds);
            }
            else if (execution == "READY" || execution == "STOPPED")
            {
                cycleSeconds = 0;
            }

            UniEmu.State.Set("cycleSeconds", cycleSeconds);
            return Math.Round(cycleSeconds, 1);
            """,
            now.AddHours(-1).AddMinutes(3));

        yield return CreateSharedScript("scr-cnc-axis-load", "cnc/axis-load.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            var baseLoad = ToDouble(UniEmu.Tag.Value, 15);
            var spindleLoad = ReadNumber("SpindleLoadPct", 0);
            var actualFeed = ReadNumber("ActualFeedMmMin", 0);
            var distanceToGo = ReadNumber("DistanceToGo", 0);
            var cuttingBonus = actualFeed > 0 && distanceToGo > 0
                ? 8
                : 2;

            var load = baseLoad + spindleLoad * 0.22 + actualFeed / 2500d + cuttingBonus;
            return Math.Round(Clamp(load, 0, 100), 1);
            """,
            now.AddHours(-1).AddMinutes(4));

        yield return CreateSharedScript("scr-cnc-machine-readiness", "cnc/machine-readiness.csx",
            """
            #load "math.csx"

            bool ReadBool(string key, bool fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToBool(tag?.Value, fallback)
                    : fallback;
            }

            return ReadBool("PowerState", true)
                && ReadBool("DoorClosed", true)
                && ReadBool("FixtureClamped", true)
                && ReadBool("ServoReady", true);
            """,
            now.AddHours(-1).AddMinutes(5));

        yield return CreateSharedScript("scr-cnc-tech-stop", "cnc/technological-stop.csx",
            """
            #load "math.csx"

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            return ReadText("ExecutionState", "READY") == "HOLD";
            """,
            now.AddHours(-1).AddMinutes(6));

        yield return CreateSharedScript("scr-cnc-emergency-stop", "cnc/emergency-stop.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            return ReadText("ExecutionState", "READY") == "ALARM"
                || ReadNumber("AlarmCode", 0) >= 700;
            """,
            now.AddHours(-1).AddMinutes(7));

        yield return CreateSharedScript("scr-cnc-alarm-code", "cnc/alarm-code.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            bool ReadBool(string key, bool fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToBool(tag?.Value, fallback)
                    : fallback;
            }

            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            var execution = ReadText("ExecutionState", "READY");
            var active = execution == "ACTIVE";

            if (!ReadBool("DoorClosed", true) && active)
                return 701;

            if (!ReadBool("FixtureClamped", true) && active)
                return 702;

            if (!ReadBool("ServoReady", true))
                return 740;

            if (ReadNumber("SpindleLoadPct", 0) > 95)
                return 750;

            if (ReadNumber("VibrationMmS", 0) > 7)
                return 760;

            if (execution == "ALARM")
                return 700;

            return 0;
            """,
            now.AddHours(-1).AddMinutes(8));

        yield return CreateSharedScript("scr-cnc-alarm-text", "cnc/alarm-text.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            var code = (int)Math.Round(ReadNumber("AlarmCode", 0));
            return code switch
            {
                700 => "Controller execution state is ALARM.",
                701 => "Door interlock is open during active cycle.",
                702 => "Fixture or chuck is not clamped.",
                740 => "Servo drive is not ready.",
                750 => "Spindle overload threshold exceeded.",
                760 => "Spindle vibration threshold exceeded.",
                _ => string.Empty,
            };
            """,
            now.AddHours(-1).AddMinutes(9));

        yield return CreateSharedScript("scr-cnc-warning-text", "cnc/warning-text.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            if (ReadNumber("ToolLifeRemainingPct", 100) < 12)
                return "Tool life is below planned replacement threshold.";

            if (ReadNumber("SpindleTemperatureC", 0) > 68)
                return "Spindle temperature is above normal cutting range.";

            if (ReadNumber("VibrationMmS", 0) > 5)
                return "Spindle vibration is elevated.";

            return string.Empty;
            """,
            now.AddHours(-1).AddMinutes(10));
    }

    /// <summary>
    /// Создает информационные события о подготовленных демонстрационных ЧПУ-станках.
    /// </summary>
    /// <param name="specs">Настройки демонстрационных ЧПУ-станков.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>События seed-инициализации.</returns>
    private static IEnumerable<SystemEventEntity> CreateCncSeedEvents(IEnumerable<CncSeedSpec> specs, DateTimeOffset now)
    {
        var index = 1;
        foreach (var spec in specs)
        {
            yield return new SystemEventEntity
            {
                Id = $"ev-seed-cnc-{index}",
                EmulatorId = spec.Id,
                EmulatorName = spec.Name,
                Level = UniEmuJson.EnumString(EventLevel.Info),
                Message = $"Seed: ЧПУ-станок {spec.Name} подготовлен с программой {spec.ProgramName}, сценариями осей, шпинделя и диагностикой.",
                Timestamp = now.AddSeconds(20 + index),
            };

            index++;
        }
    }

    /// <summary>
    /// Создает сценарий питания стойки.
    /// </summary>
    /// <returns>Сценарий питания.</returns>
    private static TagScenarioConfigDto CreatePowerScenario()
    {
        return CreateStaticScenario(
            "true",
            ("power-ready", "PowerOn", 30, "true"),
            ("power-cycle", "Cycle", 330, "true"),
            ("power-service", "Service", 30, "true"));
    }

    /// <summary>
    /// Создает сценарий режима контроллера.
    /// </summary>
    /// <returns>Сценарий режима контроллера.</returns>
    private static TagScenarioConfigDto CreateControllerModeScenario()
    {
        return CreateStaticScenario(
            "AUTO",
            ("mode-auto-ready", "Ready", 25, "AUTO"),
            ("mode-auto-cutting", "Cutting", 210, "AUTO"),
            ("mode-mdi-offset", "OffsetCheck", 35, "MDI"),
            ("mode-jog-inspection", "Inspection", 35, "JOG"),
            ("mode-auto-finish", "Finish", 95, "AUTO"));
    }

    /// <summary>
    /// Создает сценарий состояния выполнения программы.
    /// </summary>
    /// <returns>Сценарий состояния выполнения.</returns>
    private static TagScenarioConfigDto CreateExecutionStateScenario()
    {
        return CreateStaticScenario(
            "READY",
            ("execution-ready", "Ready", 20, "READY"),
            ("execution-active-roughing", "Roughing", 170, "ACTIVE"),
            ("execution-hold", "FeedHold", 25, "HOLD"),
            ("execution-active-finishing", "Finishing", 130, "ACTIVE"),
            ("execution-alarm", "AlarmCheck", 15, "ALARM"),
            ("execution-stopped", "Stopped", 30, "STOPPED"));
    }

    /// <summary>
    /// Создает сценарий состояния цикла.
    /// </summary>
    /// <returns>Сценарий состояния цикла.</returns>
    private static TagScenarioConfigDto CreateCycleStateScenario()
    {
        return CreateStaticScenario(
            "Reset",
            ("cycle-reset", "Reset", 20, "Reset"),
            ("cycle-start-roughing", "Cycle Start", 170, "Cycle Start"),
            ("cycle-feed-hold", "Feed Hold", 25, "Feed Hold"),
            ("cycle-start-finishing", "Cycle Start", 130, "Cycle Start"),
            ("cycle-reset-alarm", "Reset", 45, "Reset"));
    }

    /// <summary>
    /// Создает сценарий состояния дверей.
    /// </summary>
    /// <returns>Сценарий закрытия дверей.</returns>
    private static TagScenarioConfigDto CreateDoorClosedScenario()
    {
        return CreateStaticScenario(
            "true",
            ("door-setup", "Setup", 18, "false"),
            ("door-cycle", "Cycle", 345, "true"),
            ("door-unload", "Unload", 27, "false"));
    }

    /// <summary>
    /// Создает сценарий состояния зажима.
    /// </summary>
    /// <returns>Сценарий зажима детали.</returns>
    private static TagScenarioConfigDto CreateFixtureClampedScenario()
    {
        return CreateStaticScenario(
            "true",
            ("fixture-loading", "Loading", 20, "false"),
            ("fixture-clamped", "Clamped", 335, "true"),
            ("fixture-unclamp", "Unclamp", 35, "false"));
    }

    /// <summary>
    /// Создает сценарий готовности сервоприводов.
    /// </summary>
    /// <returns>Сценарий готовности сервоприводов.</returns>
    private static TagScenarioConfigDto CreateServoReadyScenario()
    {
        return CreateStaticScenario(
            "true",
            ("servo-ready-start", "Ready", 345, "true"),
            ("servo-fault-check", "FaultCheck", 15, "false"),
            ("servo-ready-recover", "Recover", 30, "true"));
    }

    /// <summary>
    /// Создает сценарий координаты оси.
    /// </summary>
    /// <param name="axis">Имя оси для идентификаторов участков.</param>
    /// <param name="start">Начальная позиция.</param>
    /// <param name="finish">Конечная позиция.</param>
    /// <returns>Сценарий движения оси.</returns>
    private static TagScenarioConfigDto CreateAxisScenario(string axis, double start, double finish)
    {
        var middle = start + (finish - start) * 0.45;
        return new TagScenarioConfigDto(
            [
                LineSegment($"{axis}-rapid-approach", "RapidApproach", 45, start, middle),
                SineSegment($"{axis}-cutting-wave", "Cutting", 150, middle, Math.Max(0.5, Math.Abs(finish - start) * 0.015), 24),
                CurveSegment($"{axis}-finish-pass", "FinishPass", 90, middle, finish, 1.8),
                StaticSegment($"{axis}-measure", "Measure", 35, Invariant(finish)),
                LineSegment($"{axis}-return", "Return", 70, finish, start),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(start));
    }

    /// <summary>
    /// Создает сценарий остатка перемещения.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <returns>Сценарий остатка перемещения.</returns>
    private static TagScenarioConfigDto CreateDistanceToGoScenario(CncSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                LineSegment("dtg-approach", "RapidApproach", 45, spec.DistanceMax, spec.DistanceMax * 0.55),
                LineSegment("dtg-cutting", "Cutting", 150, spec.DistanceMax * 0.55, spec.DistanceMax * 0.12),
                LineSegment("dtg-finish", "FinishPass", 90, spec.DistanceMax * 0.12, 0),
                StaticSegment("dtg-measure", "Measure", 35, "0"),
                LineSegment("dtg-return", "Return", 70, spec.DistanceMax * 0.35, 0),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(spec.DistanceMax));
    }

    /// <summary>
    /// Создает сценарий командных оборотов шпинделя.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <returns>Сценарий командной скорости шпинделя.</returns>
    private static TagScenarioConfigDto CreateSpindleCommandScenario(CncSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("spindle-stop-setup", "Setup", 20, "0"),
                LineSegment("spindle-acceleration", "Acceleration", 30, 0, spec.SpindleCommand),
                StaticSegment("spindle-cutting", "Cutting", 250, Invariant(spec.SpindleCommand)),
                StaticSegment("spindle-hold", "Hold", 25, Invariant(spec.SpindleCommand * 0.6)),
                StaticSegment("spindle-stop", "Stop", 65, "0"),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(spec.SpindleCommand));
    }

    /// <summary>
    /// Создает сценарий направления вращения шпинделя.
    /// </summary>
    /// <returns>Сценарий направления шпинделя.</returns>
    private static TagScenarioConfigDto CreateSpindleDirectionScenario()
    {
        return CreateStaticScenario(
            "CW",
            ("spindle-dir-stop-setup", "Setup", 20, "STOPPED"),
            ("spindle-dir-cw", "Clockwise", 280, "CW"),
            ("spindle-dir-ccw", "ReverseTap", 25, "CCW"),
            ("spindle-dir-stop", "Stop", 65, "STOPPED"));
    }

    /// <summary>
    /// Создает сценарий командной подачи.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <returns>Сценарий командной подачи.</returns>
    private static TagScenarioConfigDto CreateCommandFeedScenario(CncSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("feed-rapid", "Rapid", 45, Invariant(spec.CommandFeed * 1.8)),
                StaticSegment("feed-roughing", "Roughing", 150, Invariant(spec.CommandFeed)),
                StaticSegment("feed-hold", "Hold", 25, "0"),
                StaticSegment("feed-finishing", "Finishing", 90, Invariant(spec.CommandFeed * 0.55)),
                StaticSegment("feed-return", "Return", 80, Invariant(spec.CommandFeed * 1.2)),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(spec.CommandFeed));
    }

    /// <summary>
    /// Создает сценарий активного режима интерполяции.
    /// </summary>
    /// <returns>Сценарий G-кодов движения.</returns>
    private static TagScenarioConfigDto CreateMotionModeScenario()
    {
        return CreateStaticScenario(
            "G00",
            ("motion-rapid", "Rapid", 45, "G00"),
            ("motion-linear", "Linear", 150, "G01"),
            ("motion-hold", "Hold", 25, "G01"),
            ("motion-arc-cw", "ArcCW", 60, "G02"),
            ("motion-arc-ccw", "ArcCCW", 30, "G03"),
            ("motion-return", "Return", 80, "G00"));
    }

    /// <summary>
    /// Создает сценарий активного инструмента.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <returns>Сценарий номера активного инструмента.</returns>
    private static TagScenarioConfigDto CreateActiveToolScenario(CncSeedSpec spec)
    {
        var finishingTool = spec.ActiveTool + 1;
        return CreateStaticScenario(
            Invariant(spec.ActiveTool),
            ("tool-setup", "Setup", 45, Invariant(spec.ActiveTool)),
            ("tool-roughing", "Roughing", 170, Invariant(spec.ActiveTool)),
            ("tool-change", "ToolChange", 25, Invariant(finishingTool)),
            ("tool-finishing", "Finishing", 150, Invariant(finishingTool)));
    }

    /// <summary>
    /// Создает строковый или булев сценарий из статических участков.
    /// </summary>
    /// <param name="startValue">Начальное значение сценария.</param>
    /// <param name="segments">Участки сценария.</param>
    /// <returns>Конфигурация сценария.</returns>
    private static TagScenarioConfigDto CreateStaticScenario(
        string startValue,
        params (string Id, string Label, int Duration, string Value)[] segments)
    {
        return new TagScenarioConfigDto(
            segments
                .Select(segment => StaticSegment(segment.Id, segment.Label, segment.Duration, segment.Value))
                .ToArray(),
            ContinueOnFormulaEnd.Repeat,
            startValue);
    }

    /// <summary>
    /// Создает синусоидальный генератор.
    /// </summary>
    /// <param name="start">Базовое значение.</param>
    /// <param name="amplitude">Амплитуда колебания.</param>
    /// <param name="period">Период колебания в секундах.</param>
    /// <param name="distortion">Искажение сигнала.</param>
    /// <returns>Конфигурация генератора.</returns>
    private static TagCalcConfigDto SinusoidCalc(double start, double amplitude, int period, double distortion)
    {
        return new TagCalcConfigDto(
            CalcType.Sinusoid,
            Start: Invariant(start),
            Finish: null,
            Duration: period,
            Amplitude: amplitude,
            Period: period,
            Curvature: null,
            Distortion: distortion);
    }

    /// <summary>
    /// Создает генератор последовательности числовых значений.
    /// </summary>
    /// <param name="values">Значения последовательности.</param>
    /// <returns>Конфигурация генератора.</returns>
    private static TagCalcConfigDto SequenceCalc(params double[] values)
    {
        return new TagCalcConfigDto(
            CalcType.Sequence,
            Start: UniEmuJson.Serialize(values),
            Finish: null,
            Duration: Math.Max(1, values.Length * 45),
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null);
    }

    /// <summary>
    /// Возвращает текст seed CNC-программы по имени файла.
    /// </summary>
    /// <param name="programName">Имя CNC-программы.</param>
    /// <returns>Текст программы.</returns>
    private static string CreateCncProgramContent(string programName)
    {
        return programName switch
        {
            "VMC650_MAIN.NC" => """
                O6501 (VMC650 DEMO BRACKET)
                N10 G21 G17 G90 G54
                N20 T07 M06
                N30 S8200 M03
                N40 G00 X-120. Y-80. Z180.
                N50 G43 H07 Z60.
                N60 G01 Z-12. F780.
                N70 G01 X120. Y40.
                N80 G02 X180. Y90. I30. J25.
                N90 G01 X380. Y260.
                N100 G00 Z180.
                N110 M05
                N120 M30
                """,
            "LATHE200_MAIN.NC" => """
                O2002 (TURN200 DEMO SHAFT)
                N10 G21 G18 G90
                N20 T0303
                N30 G97 S3200 M03
                N40 G00 X210. Z420.
                N50 G01 X120. F420.
                N60 G01 Z60.
                N70 G02 X72. Z20. R12.
                N80 G01 X38. Z-160.
                N90 G00 X210. Z420.
                N100 M05
                N110 M30
                """,
            "ROUTER03_NESTING.NC" => """
                O0303 (ROUTER03 DEMO NEST)
                N10 G21 G17 G90 G54
                N20 T12 M06
                N30 S18000 M03
                N40 G00 X0. Y0. Z120.
                N50 G01 Z-12. F2800.
                N60 G01 X1450. F5600.
                N70 G03 X1450. Y900. I0. J450.
                N80 G01 X0. Y900.
                N90 G02 X0. Y0. I0. J-450.
                N100 G00 Z120.
                N110 M05
                N120 M30
                """,
            _ => "M30",
        };
    }

    /// <summary>
    /// Создает тег ЧПУ-станка через общую фабрику тегов.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного ЧПУ-станка.</param>
    /// <param name="idSuffix">Суффикс идентификатора тега.</param>
    /// <param name="name">Отображаемое имя тега.</param>
    /// <param name="key">Ключ тега во внешнем протоколе.</param>
    /// <param name="type">Тип значения тега.</param>
    /// <param name="source">Источник значения тега.</param>
    /// <param name="preview">Начальное preview-значение.</param>
    /// <param name="calc">Конфигурация генератора.</param>
    /// <param name="formula">Конфигурация скрипта или formula-script.</param>
    /// <param name="scenario">Конфигурация сценария.</param>
    /// <param name="roundDigits">Количество знаков округления.</param>
    /// <param name="specialParameter">Специальный параметр Dispatcher.</param>
    /// <param name="description">Описание тега.</param>
    /// <returns>Сущность тега.</returns>
    private static EmulatorTagEntity CreateTag(
        CncSeedSpec spec,
        string idSuffix,
        string name,
        string key,
        TagType type,
        TagSource source,
        string preview,
        TagCalcConfigDto? calc = null,
        TagFormulaConfigDto? formula = null,
        TagScenarioConfigDto? scenario = null,
        int? roundDigits = null,
        SpecialParameter? specialParameter = null,
        string? description = null)
    {
        return CreateTag(spec.Id, idSuffix, name, key, type, source, preview, calc, formula, scenario, roundDigits, specialParameter, description);
    }

    /// <summary>
    /// Настройки демонстрационного ЧПУ-станка.
    /// </summary>
    /// <param name="Id">Идентификатор эмулятора.</param>
    /// <param name="Name">Имя эмулятора.</param>
    /// <param name="ProtocolId">Идентификатор протокола Dispatcher.</param>
    /// <param name="IntervalSec">Интервал публикации телеметрии.</param>
    /// <param name="TotalRequests">Начальный счетчик публикаций.</param>
    /// <param name="ProgramName">Имя основной CNC-программы.</param>
    /// <param name="Model">Модель стойки УЧПУ.</param>
    /// <param name="FirmwareVersion">Версия прошивки стойки.</param>
    /// <param name="SerialNumber">Серийный номер станка или стойки.</param>
    /// <param name="PlcVersion">Версия PLC-проекта.</param>
    /// <param name="ActiveTool">Начальный активный инструмент.</param>
    /// <param name="CommandFeed">Базовая командная подача.</param>
    /// <param name="FeedOverrideBase">Базовая коррекция подачи.</param>
    /// <param name="RapidOverrideBase">Базовая коррекция быстрых перемещений.</param>
    /// <param name="SpindleCommand">Командная скорость шпинделя.</param>
    /// <param name="SpindleActualBase">Базовая фактическая скорость шпинделя.</param>
    /// <param name="SpindleAmplitude">Амплитуда изменения фактической скорости шпинделя.</param>
    /// <param name="SpindleLoadBase">Базовая нагрузка шпинделя.</param>
    /// <param name="SpindleLoadAmplitude">Амплитуда изменения нагрузки шпинделя.</param>
    /// <param name="SpindleTemperatureBase">Базовая температура шпинделя.</param>
    /// <param name="AxisLoadBase">Базовая нагрузка осей.</param>
    /// <param name="VibrationBase">Базовая вибрация шпинделя.</param>
    /// <param name="XStart">Начальная позиция X.</param>
    /// <param name="XFinish">Конечная позиция X.</param>
    /// <param name="YStart">Начальная позиция Y.</param>
    /// <param name="YFinish">Конечная позиция Y.</param>
    /// <param name="ZStart">Начальная позиция Z.</param>
    /// <param name="ZFinish">Конечная позиция Z.</param>
    /// <param name="DistanceMax">Максимальный остаток перемещения.</param>
    private sealed record CncSeedSpec(
        string Id,
        string Name,
        int ProtocolId,
        int IntervalSec,
        long TotalRequests,
        string ProgramName,
        string Model,
        string FirmwareVersion,
        string SerialNumber,
        string PlcVersion,
        int ActiveTool,
        double CommandFeed,
        double FeedOverrideBase,
        double RapidOverrideBase,
        double SpindleCommand,
        double SpindleActualBase,
        double SpindleAmplitude,
        double SpindleLoadBase,
        double SpindleLoadAmplitude,
        double SpindleTemperatureBase,
        double AxisLoadBase,
        double VibrationBase,
        double XStart,
        double XFinish,
        double YStart,
        double YFinish,
        double ZStart,
        double ZFinish,
        double DistanceMax);
}
