using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// Содержит seed-данные термических печей.
/// </summary>
public static partial class UniEmuSeeder
{
    /// <summary>
    /// Возвращает набор демонстрационных термических печей с разными температурными профилями.
    /// </summary>
    /// <returns>Список настроек печей для начального заполнения.</returns>
    private static IReadOnlyList<FurnaceSeedSpec> CreateFurnaceSpecs()
    {
        return
        [
            new FurnaceSeedSpec(
                Id: "em-8a2d4a98a",
                Name: "Печь цементации 1",
                ProtocolId: 31,
                IntervalSec: 2,
                TotalRequests: 0,
                AmbientTemperature: 32,
                ProcessSetpoint: 920,
                DoorOpenTemperature: 760,
                HighSetpoint: 950,
                CoolingTemperature: 140,
                ProgramName: "CARB-920-950-A",
                FanBase: 72,
                FanAmplitude: 8,
                FanPeriodSec: 90),
            new FurnaceSeedSpec(
                Id: "em-a9d16c278",
                Name: "Печь отпуска 2",
                ProtocolId: 32,
                IntervalSec: 2,
                TotalRequests: 0,
                AmbientTemperature: 28,
                ProcessSetpoint: 540,
                DoorOpenTemperature: 430,
                HighSetpoint: 610,
                CoolingTemperature: 80,
                ProgramName: "TEMP-540-610-B",
                FanBase: 58,
                FanAmplitude: 6,
                FanPeriodSec: 75),
            new FurnaceSeedSpec(
                Id: "em-c55895497",
                Name: "Печь пайки 3",
                ProtocolId: 33,
                IntervalSec: 1,
                TotalRequests: 0,
                AmbientTemperature: 34,
                ProcessSetpoint: 780,
                DoorOpenTemperature: 620,
                HighSetpoint: 840,
                CoolingTemperature: 110,
                ProgramName: "BRAZE-780-840-C",
                FanBase: 66,
                FanAmplitude: 7,
                FanPeriodSec: 60),
        ];
    }

    /// <summary>
    /// Создает строку эмулятора печи с базовой runtime-статистикой.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <param name="targetUrl">URL целевой системы для эмулятора.</param>
    /// <returns>Сущность эмулятора печи.</returns>
    private static EmulatorEntity CreateFurnaceEmulator(FurnaceSeedSpec spec, DateTimeOffset now, string targetUrl)
    {
        return new EmulatorEntity
        {
            Id = spec.Id,
            Name = spec.Name,
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = spec.ProtocolId,
            TargetUrl = targetUrl,
            IntervalSec = spec.IntervalSec,
            LastRun = now.AddMinutes(-12),
            NextRun = now.AddSeconds(spec.IntervalSec),
            TotalRequests = spec.TotalRequests,
        };
    }

    /// <summary>
    /// Создает полный набор тегов печи: сценарии, генератор, скрипты и static-метаданные.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <returns>Теги для указанной печи.</returns>
    private static IEnumerable<EmulatorTagEntity> CreateFurnaceTags(FurnaceSeedSpec spec)
    {
        yield return CreateTag(
            spec,
            "temperature",
            "Температура",
            "Temperature",
            TagType.Double,
            TagSource.Scenario,
            Invariant(spec.AmbientTemperature),
            scenario: CreateTemperatureScenario(spec),
            roundDigits: 1,
            description: "Фактическая температура камеры: нагрев, выдержка, просадка при открытии дверцы, второй режим и охлаждение.");

        yield return CreateTag(
            spec,
            "setpoint",
            "Уставка",
            "Setpoint",
            TagType.Double,
            TagSource.Scenario,
            Invariant(spec.ProcessSetpoint),
            scenario: CreateSetpointScenario(spec),
            roundDigits: 1,
            description: "Температурная уставка, которая переходит с основного режима на более высокий второй режим.");

        yield return CreateTag(
            spec,
            "work-mode",
            "Режим работы",
            "WorkMode",
            TagType.String,
            TagSource.Scenario,
            "Heating",
            scenario: CreateWorkModeScenario(),
            specialParameter: SpecialParameter.WorkMode,
            description: "Текущий режим работы печи, включая открытие дверцы для замены детали.");

        yield return CreateTag(
            spec,
            "door-open",
            "Дверца открыта",
            "DoorOpen",
            TagType.Bool,
            TagSource.Scenario,
            "false",
            scenario: CreateDoorOpenScenario(),
            description: "Флаг открытой дверцы на участке замены детали.");

        yield return CreateTag(
            spec,
            "heater-power",
            "Мощность нагрева",
            "HeaterPowerPct",
            TagType.Double,
            TagSource.Script,
            "0",
            formula: ScriptFormula(FurnaceScriptId(spec, "heater-power")),
            roundDigits: 1,
            description: "Мощность нагревателей, рассчитанная CSX-скриптом по температуре, уставке и состоянию дверцы.");

        yield return CreateTag(
            spec,
            "fan-speed",
            "Скорость вентилятора",
            "FanSpeedPct",
            TagType.Double,
            TagSource.Generator,
            Invariant(spec.FanBase),
            calc: new TagCalcConfigDto(
                CalcType.Sinusoid,
                Start: Invariant(spec.FanBase),
                Finish: null,
                Duration: null,
                Amplitude: spec.FanAmplitude,
                Period: spec.FanPeriodSec,
                Curvature: null,
                Distortion: 1.5),
            roundDigits: 1,
            description: "Скорость циркуляционного вентилятора, заданная генератором по синусоидальной формуле.");

        yield return CreateTag(
            spec,
            "temperature-deviation",
            "Отклонение",
            "TemperatureDeviation",
            TagType.Double,
            TagSource.FormulaScript,
            "0",
            calc: FlatLineCalc(),
            formula: ScriptFormula(FurnaceScriptId(spec, "deviation")),
            roundDigits: 1,
            description: "Отклонение фактической температуры от уставки: генераторная формула с постобработкой CSX-скриптом.");

        yield return CreateTag(
            spec,
            "alarm-code",
            "Код аварии",
            "AlarmCode",
            TagType.Int,
            TagSource.Script,
            "0",
            formula: ScriptFormula(FurnaceScriptId(spec, "alarm-code")),
            specialParameter: SpecialParameter.ErrorNum,
            description: "Код технологического предупреждения, вычисляемый скриптом по отклонению температуры и открытой дверце.");

        yield return CreateTag(
            spec,
            "program-name",
            "Программа",
            "ProgramName",
            TagType.String,
            TagSource.Static,
            spec.ProgramName,
            specialParameter: SpecialParameter.PrgName,
            description: "Название термического рецепта, передаваемое как строковый технологический параметр.");
    }

    /// <summary>
    /// Создает сценарий фактической температуры камеры.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <returns>Сценарий температуры.</returns>
    private static TagScenarioConfigDto CreateTemperatureScenario(FurnaceSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                LineSegment("temperature-heating", "Нагрев", 300, spec.AmbientTemperature, spec.ProcessSetpoint),
                SineSegment("temperature-soaking", "Выдержка", 360, spec.ProcessSetpoint, amplitude: 4, period: 120),
                LineSegment("temperature-door-open", "Замена детали", 90, spec.ProcessSetpoint, spec.DoorOpenTemperature),
                LineSegment("temperature-high-ramp", "Разгон до высокой температуры", 180, spec.DoorOpenTemperature, spec.HighSetpoint),
                SineSegment("temperature-high-soak", "Высокотемпературная выдержка", 300, spec.HighSetpoint, amplitude: 3, period: 90),
                LineSegment("temperature-cooling", "Охлаждение", 240, spec.HighSetpoint, spec.CoolingTemperature),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(spec.AmbientTemperature));
    }

    /// <summary>
    /// Создает сценарий температурной уставки.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <returns>Сценарий уставки.</returns>
    private static TagScenarioConfigDto CreateSetpointScenario(FurnaceSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("setpoint-heating", "Нагрев", 300, Invariant(spec.ProcessSetpoint)),
                StaticSegment("setpoint-soaking", "Выдержка", 360, Invariant(spec.ProcessSetpoint)),
                StaticSegment("setpoint-door-open", "Замена детали", 90, Invariant(spec.DoorOpenTemperature)),
                StaticSegment("setpoint-high-ramp", "Разгон до высокой температуры", 180, Invariant(spec.HighSetpoint)),
                StaticSegment("setpoint-high-soak", "Высокотемпературная выдержка", 300, Invariant(spec.HighSetpoint)),
                StaticSegment("setpoint-cooling", "Охлаждение", 240, Invariant(spec.CoolingTemperature)),
            ],
            ContinueOnFormulaEnd.Repeat,
            Invariant(spec.ProcessSetpoint));
    }

    /// <summary>
    /// Создает строковый сценарий режима работы.
    /// </summary>
    /// <returns>Сценарий режима работы печи.</returns>
    private static TagScenarioConfigDto CreateWorkModeScenario()
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("mode-heating", "Нагрев", 300, "Heating"),
                StaticSegment("mode-soaking", "Выдержка", 360, "Soaking"),
                StaticSegment("mode-door-open", "Замена детали", 90, "DoorOpenPartChange"),
                StaticSegment("mode-high-ramp", "Разгон до высокой температуры", 180, "HighTempRamp"),
                StaticSegment("mode-high-soak", "Высокотемпературная выдержка", 300, "HighTempSoak"),
                StaticSegment("mode-cooling", "Охлаждение", 240, "Cooling"),
            ],
            ContinueOnFormulaEnd.Repeat,
            "Heating");
    }

    /// <summary>
    /// Создает булев сценарий состояния дверцы.
    /// </summary>
    /// <returns>Сценарий открытия дверцы.</returns>
    private static TagScenarioConfigDto CreateDoorOpenScenario()
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("door-heating", "Нагрев", 300, "false"),
                StaticSegment("door-soaking", "Выдержка", 360, "false"),
                StaticSegment("door-open", "Замена детали", 90, "true"),
                StaticSegment("door-high-ramp", "Разгон до высокой температуры", 180, "false"),
                StaticSegment("door-high-soak", "Высокотемпературная выдержка", 300, "false"),
                StaticSegment("door-cooling", "Охлаждение", 240, "false"),
            ],
            ContinueOnFormulaEnd.Repeat,
            "false");
    }

    /// <summary>
    /// Возвращает идентификатор scoped-скрипта печи.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <param name="scriptName">Короткое имя скрипта без расширения.</param>
    /// <returns>Идентификатор скрипта.</returns>
    private static string FurnaceScriptId(FurnaceSeedSpec spec, string scriptName)
    {
        return $"scr-{spec.Id["em-".Length..]}-{scriptName}";
    }

    /// <summary>
    /// Создает CSX-скрипты, привязанные к конкретной печи.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция scoped-скриптов печи.</returns>
    private static IEnumerable<ScriptFileEntity> CreateFurnaceScripts(FurnaceSeedSpec spec, DateTimeOffset now)
    {
        yield return CreateEmulatorScript(
            FurnaceScriptId(spec, "heater-power"),
            "heater-power.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var temperature = ReadNumber("Temperature", 25);
            var setpoint = ReadNumber("Setpoint", temperature);
            var doorOpen = ReadBool("DoorOpen", false);

            if (doorOpen)
                return 0d;

            var error = setpoint - temperature;
            var power = error > 0
                ? 35 + error * 0.18
                : 8 + error * 0.05;

            return Math.Round(Clamp(power, 0, 100), 1);
            """,
            now.AddHours(-2).AddMinutes(5));

        yield return CreateEmulatorScript(
            FurnaceScriptId(spec, "deviation"),
            "deviation.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var fallback = ToDouble(UniEmu.Tag.Value, 0);
            var temperature = ReadNumber("Temperature", fallback);
            var setpoint = ReadNumber("Setpoint", temperature);

            return Math.Round(temperature - setpoint, 1);
            """,
            now.AddHours(-2).AddMinutes(10));

        yield return CreateEmulatorScript(
            FurnaceScriptId(spec, "alarm-code"),
            "alarm-code.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var temperature = ReadNumber("Temperature", 25);
            var setpoint = ReadNumber("Setpoint", temperature);
            var doorOpen = ReadBool("DoorOpen", false);
            var deviation = Math.Abs(temperature - setpoint);

            if (doorOpen && temperature > 300)
                return 210;

            if (deviation > 35)
                return 110;

            return 0;
            """,
            now.AddHours(-2).AddMinutes(15));
    }

    /// <summary>
    /// Создает информационные события о подготовленных демонстрационных печах.
    /// </summary>
    /// <param name="specs">Настройки демонстрационных печей.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>События seed-инициализации.</returns>
    private static IEnumerable<SystemEventEntity> CreateFurnaceSeedEvents(IEnumerable<FurnaceSeedSpec> specs, DateTimeOffset now)
    {
        var index = 1;
        foreach (var spec in specs)
        {
            yield return new SystemEventEntity
            {
                Id = $"ev-seed-furnace-{index}",
                EmulatorId = spec.Id,
                EmulatorName = spec.Name,
                Level = UniEmuJson.EnumString(EventLevel.Info),
                Message = $"Термическая печь {spec.Name} подготовлена с циклом нагрева, замены детали, второго режима и охлаждения.",
                Timestamp = now.AddSeconds(index),
            };

            index++;
        }
    }

    /// <summary>
    /// Создает тег печи через общую фабрику тегов.
    /// </summary>
    /// <param name="spec">Настройки демонстрационной печи.</param>
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
        FurnaceSeedSpec spec,
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
    /// Настройки демонстрационной термической печи.
    /// </summary>
    /// <param name="Id">Идентификатор эмулятора.</param>
    /// <param name="Name">Имя эмулятора.</param>
    /// <param name="ProtocolId">Идентификатор протокола Dispatcher.</param>
    /// <param name="IntervalSec">Интервал публикации телеметрии.</param>
    /// <param name="TotalRequests">Начальный счетчик публикаций.</param>
    /// <param name="AmbientTemperature">Начальная температура камеры.</param>
    /// <param name="ProcessSetpoint">Основная уставка.</param>
    /// <param name="DoorOpenTemperature">Температура после открытия дверцы.</param>
    /// <param name="HighSetpoint">Уставка второго, более горячего режима.</param>
    /// <param name="CoolingTemperature">Температура конца охлаждения.</param>
    /// <param name="ProgramName">Название термического рецепта.</param>
    /// <param name="FanBase">Базовая скорость вентилятора.</param>
    /// <param name="FanAmplitude">Амплитуда изменения скорости вентилятора.</param>
    /// <param name="FanPeriodSec">Период изменения скорости вентилятора.</param>
    private sealed record FurnaceSeedSpec(
        string Id,
        string Name,
        int ProtocolId,
        int IntervalSec,
        long TotalRequests,
        double AmbientTemperature,
        double ProcessSetpoint,
        double DoorOpenTemperature,
        double HighSetpoint,
        double CoolingTemperature,
        string ProgramName,
        double FanBase,
        double FanAmplitude,
        int FanPeriodSec);
}
