using System.Globalization;
using System.Text;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// Создает демонстрационные данные в пустой базе UniEmu.
/// </summary>
public static class UniEmuSeeder
{
    /// <summary>
    /// Заполняет пустую базу термическими печами, тегами, сценариями, генераторами, скриптами и событиями.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="cancellationToken">Токен отмены операции сохранения.</param>
    /// <returns>Задача инициализации seed-данных.</returns>
    public static async Task SeedAsync(UniEmuDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Emulators.Any())
            return;

        var now = DateTimeOffset.UtcNow;
        var furnaces = CreateFurnaceSpecs();
        var emulators = furnaces.Select(spec => CreateFurnaceEmulator(spec, now)).ToArray();

        db.Emulators.AddRange(emulators);
        db.EmulatorTags.AddRange(furnaces.SelectMany(CreateFurnaceTags));
        db.ScriptFiles.AddRange(CreateSharedFurnaceScripts(now));
        db.SystemEvents.AddRange(CreateSeedEvents(furnaces, now));

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Возвращает набор демонстрационных термических печей с разными температурными профилями.
    /// </summary>
    /// <returns>Список настроек печей для начального заполнения.</returns>
    private static IReadOnlyList<FurnaceSeedSpec> CreateFurnaceSpecs()
    {
        return
        [
            new FurnaceSeedSpec(
                Id: "em-furnace-carburizing-01",
                Name: "Furnace_Carburizing_01",
                ProtocolId: 31,
                IntervalSec: 2,
                TotalRequests: 18420,
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
                Id: "em-furnace-tempering-02",
                Name: "Furnace_Tempering_02",
                ProtocolId: 32,
                IntervalSec: 2,
                TotalRequests: 12375,
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
                Id: "em-furnace-brazing-03",
                Name: "Furnace_Brazing_03",
                ProtocolId: 33,
                IntervalSec: 1,
                TotalRequests: 29710,
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
    /// <returns>Сущность эмулятора печи.</returns>
    private static EmulatorEntity CreateFurnaceEmulator(FurnaceSeedSpec spec, DateTimeOffset now)
    {
        return new EmulatorEntity
        {
            Id = spec.Id,
            Name = spec.Name,
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = spec.ProtocolId,
            TargetUrl = "https://scada.local/api/thermal/ingest",
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
            "Temperature",
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
            "Setpoint",
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
            "WorkMode",
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
            "DoorOpen",
            "DoorOpen",
            TagType.Bool,
            TagSource.Scenario,
            "false",
            scenario: CreateDoorOpenScenario(),
            description: "Флаг открытой дверцы на участке замены детали.");

        yield return CreateTag(
            spec,
            "heater-power",
            "HeaterPowerPct",
            "HeaterPowerPct",
            TagType.Double,
            TagSource.Script,
            "0",
            formula: ScriptFormula("scr-furnace-heater-power"),
            roundDigits: 1,
            description: "Мощность нагревателей, рассчитанная CSX-скриптом по температуре, уставке и состоянию дверцы.");

        yield return CreateTag(
            spec,
            "fan-speed",
            "FanSpeedPct",
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
            "TemperatureDeviation",
            "TemperatureDeviation",
            TagType.Double,
            TagSource.FormulaScript,
            "0",
            calc: FlatLineCalc(),
            formula: ScriptFormula("scr-furnace-deviation"),
            roundDigits: 1,
            description: "Отклонение фактической температуры от уставки: генераторная формула с постобработкой CSX-скриптом.");

        yield return CreateTag(
            spec,
            "alarm-code",
            "AlarmCode",
            "AlarmCode",
            TagType.Int,
            TagSource.Script,
            "0",
            formula: ScriptFormula("scr-furnace-alarm-code"),
            specialParameter: SpecialParameter.ErrorNum,
            description: "Код технологического предупреждения, вычисляемый скриптом по отклонению температуры и открытой дверце.");

        yield return CreateTag(
            spec,
            "program-name",
            "ProgramName",
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
                LineSegment("temperature-heating", "Heating", 300, spec.AmbientTemperature, spec.ProcessSetpoint),
                SineSegment("temperature-soaking", "Soaking", 360, spec.ProcessSetpoint, amplitude: 4, period: 120),
                LineSegment("temperature-door-open", "DoorOpenPartChange", 90, spec.ProcessSetpoint, spec.DoorOpenTemperature),
                LineSegment("temperature-high-ramp", "HighTempRamp", 180, spec.DoorOpenTemperature, spec.HighSetpoint),
                SineSegment("temperature-high-soak", "HighTempSoak", 300, spec.HighSetpoint, amplitude: 3, period: 90),
                LineSegment("temperature-cooling", "Cooling", 240, spec.HighSetpoint, spec.CoolingTemperature),
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
                StaticSegment("setpoint-heating", "Heating", 300, Invariant(spec.ProcessSetpoint)),
                StaticSegment("setpoint-soaking", "Soaking", 360, Invariant(spec.ProcessSetpoint)),
                StaticSegment("setpoint-door-open", "DoorOpenPartChange", 90, Invariant(spec.DoorOpenTemperature)),
                StaticSegment("setpoint-high-ramp", "HighTempRamp", 180, Invariant(spec.HighSetpoint)),
                StaticSegment("setpoint-high-soak", "HighTempSoak", 300, Invariant(spec.HighSetpoint)),
                StaticSegment("setpoint-cooling", "Cooling", 240, Invariant(spec.CoolingTemperature)),
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
                StaticSegment("mode-heating", "Heating", 300, "Heating"),
                StaticSegment("mode-soaking", "Soaking", 360, "Soaking"),
                StaticSegment("mode-door-open", "DoorOpenPartChange", 90, "DoorOpenPartChange"),
                StaticSegment("mode-high-ramp", "HighTempRamp", 180, "HighTempRamp"),
                StaticSegment("mode-high-soak", "HighTempSoak", 300, "HighTempSoak"),
                StaticSegment("mode-cooling", "Cooling", 240, "Cooling"),
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
                StaticSegment("door-heating", "Heating", 300, "false"),
                StaticSegment("door-soaking", "Soaking", 360, "false"),
                StaticSegment("door-open", "DoorOpenPartChange", 90, "true"),
                StaticSegment("door-high-ramp", "HighTempRamp", 180, "false"),
                StaticSegment("door-high-soak", "HighTempSoak", 300, "false"),
                StaticSegment("door-cooling", "Cooling", 240, "false"),
            ],
            ContinueOnFormulaEnd.Repeat,
            "false");
    }

    /// <summary>
    /// Создает shared CSX-скрипты, используемые script и formula-script тегами печей.
    /// </summary>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция shared-скриптов.</returns>
    private static IEnumerable<ScriptFileEntity> CreateSharedFurnaceScripts(DateTimeOffset now)
    {
        yield return CreateSharedScript(
            "scr-furnace-math",
            "furnace/math.csx",
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
            """,
            now.AddHours(-2));

        yield return CreateSharedScript(
            "scr-furnace-heater-power",
            "furnace/heater-power.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            bool ReadBool(string key)
            {
                if (!UniEmu.Tags.TryGetValue(key, out var tag) || tag?.Value is null)
                    return false;

                return tag.Value switch
                {
                    bool boolValue => boolValue,
                    string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                    _ => ToDouble(tag.Value, 0) != 0,
                };
            }

            var temperature = ReadNumber("Temperature", 25);
            var setpoint = ReadNumber("Setpoint", temperature);
            var doorOpen = ReadBool("DoorOpen");

            if (doorOpen)
                return 0d;

            var error = setpoint - temperature;
            var power = error > 0
                ? 35 + error * 0.18
                : 8 + error * 0.05;

            return Math.Round(Clamp(power, 0, 100), 1);
            """,
            now.AddHours(-2).AddMinutes(5));

        yield return CreateSharedScript(
            "scr-furnace-deviation",
            "furnace/deviation.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            var fallback = ToDouble(UniEmu.Tag.Value, 0);
            var temperature = ReadNumber("Temperature", fallback);
            var setpoint = ReadNumber("Setpoint", temperature);

            return Math.Round(temperature - setpoint, 1);
            """,
            now.AddHours(-2).AddMinutes(10));

        yield return CreateSharedScript(
            "scr-furnace-alarm-code",
            "furnace/alarm-code.csx",
            """
            #load "math.csx"

            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            bool ReadBool(string key)
            {
                if (!UniEmu.Tags.TryGetValue(key, out var tag) || tag?.Value is null)
                    return false;

                return tag.Value switch
                {
                    bool boolValue => boolValue,
                    string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                    _ => ToDouble(tag.Value, 0) != 0,
                };
            }

            var temperature = ReadNumber("Temperature", 25);
            var setpoint = ReadNumber("Setpoint", temperature);
            var doorOpen = ReadBool("DoorOpen");
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
    private static IEnumerable<SystemEventEntity> CreateSeedEvents(IEnumerable<FurnaceSeedSpec> specs, DateTimeOffset now)
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
                Message = $"Seed: термическая печь {spec.Name} подготовлена с циклом нагрева, замены детали, второго режима и охлаждения.",
                Timestamp = now.AddSeconds(index),
            };

            index++;
        }
    }

    /// <summary>
    /// Создает тег печи и сериализует связанные настройки в JSON.
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
        return new EmulatorTagEntity
        {
            Id = $"tg-{spec.Id["em-".Length..]}-{idSuffix}",
            EmulatorId = spec.Id,
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(type),
            Source = UniEmuJson.EnumString(source),
            Preview = preview,
            TriggerJson = UniEmuJson.Serialize(IntervalTrigger(1, TagIntervalUnit.Sec)),
            CalcJson = calc is null ? null : UniEmuJson.Serialize(calc),
            FormulaJson = formula is null ? null : UniEmuJson.Serialize(formula),
            ScenarioJson = scenario is null ? null : UniEmuJson.Serialize(scenario),
            RoundDigits = roundDigits,
            SpecialParameter = specialParameter is null ? null : UniEmuJson.EnumString(specialParameter.Value),
            Description = description,
        };
    }

    /// <summary>
    /// Создает shared-скрипт с корректным размером содержимого.
    /// </summary>
    /// <param name="id">Идентификатор скрипта.</param>
    /// <param name="name">Путь скрипта для редактора и директив <c>#load</c>.</param>
    /// <param name="content">Содержимое CSX-скрипта.</param>
    /// <param name="updatedAt">Время последнего обновления.</param>
    /// <returns>Сущность shared-скрипта.</returns>
    private static ScriptFileEntity CreateSharedScript(string id, string name, string content, DateTimeOffset updatedAt)
    {
        return new ScriptFileEntity
        {
            Id = id,
            Name = name,
            Scope = UniEmuJson.EnumString(ScriptScope.Shared),
            Content = content,
            UpdatedAt = updatedAt,
            SizeBytes = Encoding.UTF8.GetByteCount(content),
        };
    }

    /// <summary>
    /// Создает ссылку на сохраненный CSX-скрипт.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <returns>Конфигурация скриптовой формулы.</returns>
    private static TagFormulaConfigDto ScriptFormula(string scriptId) => new(scriptId, null);

    /// <summary>
    /// Создает плоскую линейную формулу для formula-script тега.
    /// </summary>
    /// <returns>Генератор, выдающий нулевую базовую линию.</returns>
    private static TagCalcConfigDto FlatLineCalc()
    {
        return new TagCalcConfigDto(
            CalcType.Line,
            Start: "0",
            Finish: "0",
            Duration: 60,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null);
    }

    /// <summary>
    /// Создает линейный участок сценария.
    /// </summary>
    /// <param name="id">Идентификатор участка.</param>
    /// <param name="label">Отображаемая подпись участка.</param>
    /// <param name="duration">Длительность участка в секундах.</param>
    /// <param name="start">Начальное значение.</param>
    /// <param name="finish">Конечное значение.</param>
    /// <returns>Участок сценария.</returns>
    private static TagScenarioSegmentDto LineSegment(string id, string label, int duration, double start, double finish)
    {
        return new TagScenarioSegmentDto(
            id,
            duration,
            new TagCalcConfigDto(
                CalcType.Line,
                Start: Invariant(start),
                Finish: Invariant(finish),
                Duration: duration,
                Amplitude: null,
                Period: null,
                Curvature: null,
                Distortion: null),
            label);
    }

    /// <summary>
    /// Создает синусоидальный участок сценария вокруг базового значения.
    /// </summary>
    /// <param name="id">Идентификатор участка.</param>
    /// <param name="label">Отображаемая подпись участка.</param>
    /// <param name="duration">Длительность участка в секундах.</param>
    /// <param name="start">Базовое значение.</param>
    /// <param name="amplitude">Амплитуда колебания.</param>
    /// <param name="period">Период колебания.</param>
    /// <returns>Участок сценария.</returns>
    private static TagScenarioSegmentDto SineSegment(string id, string label, int duration, double start, double amplitude, double period)
    {
        return new TagScenarioSegmentDto(
            id,
            duration,
            new TagCalcConfigDto(
                CalcType.Sinusoid,
                Start: Invariant(start),
                Finish: null,
                Duration: duration,
                Amplitude: amplitude,
                Period: period,
                Curvature: null,
                Distortion: 0.4),
            label);
    }

    /// <summary>
    /// Создает статический участок сценария.
    /// </summary>
    /// <param name="id">Идентификатор участка.</param>
    /// <param name="label">Отображаемая подпись участка.</param>
    /// <param name="duration">Длительность участка в секундах.</param>
    /// <param name="value">Статическое значение участка.</param>
    /// <returns>Участок сценария.</returns>
    private static TagScenarioSegmentDto StaticSegment(string id, string label, int duration, string value)
    {
        return new TagScenarioSegmentDto(
            id,
            duration,
            new TagCalcConfigDto(
                CalcType.Static,
                Start: value,
                Finish: null,
                Duration: duration,
                Amplitude: null,
                Period: null,
                Curvature: null,
                Distortion: null),
            label);
    }

    /// <summary>
    /// Создает периодический триггер тега.
    /// </summary>
    /// <param name="value">Значение интервала.</param>
    /// <param name="unit">Единица измерения интервала.</param>
    /// <returns>Настройки триггера.</returns>
    private static TagTriggerDto IntervalTrigger(int value, TagIntervalUnit unit)
    {
        return new TagTriggerDto(TagTriggerMode.Interval, Event: null, Cron: null, IntervalValue: value, IntervalUnit: unit);
    }

    /// <summary>
    /// Преобразует число в строку с инвариантной культурой.
    /// </summary>
    /// <param name="value">Число для сериализации в preview или формулу.</param>
    /// <returns>Строковое представление числа.</returns>
    private static string Invariant(double value) => value.ToString(CultureInfo.InvariantCulture);

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
