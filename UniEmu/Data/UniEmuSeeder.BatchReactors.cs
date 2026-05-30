using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// Содержит seed-данные batch-реактора со статичными параметрами, сценариями и скриптовыми зависимостями.
/// </summary>
public static partial class UniEmuSeeder
{
    /// <summary>
    /// Возвращает демонстрационный batch-реактор с расширенным набором статичных тегов.
    /// </summary>
    /// <returns>Список настроек batch-реакторов для начального заполнения.</returns>
    private static IReadOnlyList<BatchReactorSeedSpec> CreateBatchReactorSpecs()
    {
        return
        [
            new BatchReactorSeedSpec(
                Id: "em-b6f42a901",
                Name: "Смесительный реактор BR-101",
                ProtocolId: 51,
                IntervalSec: 1,
                TotalRequests: 0,
                UnitName: "BR-101",
                Area: "Synthesis",
                Line: "Line-A",
                EquipmentModel: "UniMix BR-5000",
                ControllerVersion: "PLC-Mix 5.12",
                RecipeName: "ALKYD-72C-PH68",
                ProductCode: "ALK-2042",
                BatchId: "BATCH-2026-0524-A",
                MaterialLot: "RM-ACID-0524-17",
                OperatorId: "OP-184",
                VesselVolumeL: 5000,
                TargetTemperatureC: 72,
                TargetPh: 6.8,
                ReactionSpeedRpm: 520,
                PressureBaseBar: 1.35),
        ];
    }

    /// <summary>
    /// Создает строку эмулятора batch-реактора с базовой runtime-статистикой.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <param name="targetUrl">URL целевой системы для эмулятора.</param>
    /// <returns>Сущность batch-реактора.</returns>
    private static EmulatorEntity CreateBatchReactorEmulator(BatchReactorSeedSpec spec, DateTimeOffset now, string targetUrl)
    {
        return new EmulatorEntity
        {
            Id = spec.Id,
            Name = spec.Name,
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = spec.ProtocolId,
            TargetUrl = targetUrl,
            IntervalSec = spec.IntervalSec,
            LastRun = now.AddMinutes(-5),
            NextRun = now.AddSeconds(spec.IntervalSec),
            TotalRequests = spec.TotalRequests,
        };
    }

    /// <summary>
    /// Создает полный набор тегов batch-реактора: статичные паспортные данные, сценарии партии и скриптовую диагностику.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <returns>Теги для указанного batch-реактора.</returns>
    private static IEnumerable<EmulatorTagEntity> CreateBatchReactorTags(BatchReactorSeedSpec spec)
    {
        yield return CreateTag(spec, "unit-name", "Узел", "UnitName", TagType.String, TagSource.Static, spec.UnitName,
            description: "Технологическое имя реактора внутри линии.");
        yield return CreateTag(spec, "area", "Участок", "Area", TagType.String, TagSource.Static, spec.Area,
            description: "Производственный участок, к которому относится реактор.");
        yield return CreateTag(spec, "line", "Линия", "Line", TagType.String, TagSource.Static, spec.Line,
            description: "Производственная линия партии.");
        yield return CreateTag(spec, "equipment-model", "Модель оборудования", "EquipmentModel", TagType.String, TagSource.Static, spec.EquipmentModel,
            description: "Паспортная модель реактора.");
        yield return CreateTag(spec, "controller-version", "Версия контроллера", "ControllerVersion", TagType.String, TagSource.Static, spec.ControllerVersion,
            specialParameter: SpecialParameter.FirmwareVersion, description: "Версия проекта PLC/контроллера batch-реактора.");
        yield return CreateTag(spec, "recipe-name", "Рецепт", "RecipeName", TagType.String, TagSource.Static, spec.RecipeName,
            specialParameter: SpecialParameter.PrgName, description: "Имя активного рецепта партии.");
        yield return CreateTag(spec, "product-code", "Код продукта", "ProductCode", TagType.String, TagSource.Static, spec.ProductCode,
            description: "Артикул продукта, который используется скриптом качества.");
        yield return CreateTag(spec, "batch-id", "Партия", "BatchId", TagType.String, TagSource.Static, spec.BatchId,
            description: "Идентификатор текущей производственной партии.");
        yield return CreateTag(spec, "material-lot", "Партия сырья", "MaterialLot", TagType.String, TagSource.Static, spec.MaterialLot,
            description: "Партия ключевого сырья, проверяемая скриптом качества.");
        yield return CreateTag(spec, "operator-id", "Оператор", "OperatorId", TagType.String, TagSource.Static, spec.OperatorId,
            description: "Идентификатор оператора смены.");
        yield return CreateTag(spec, "vessel-volume", "Объем реактора", "VesselVolumeL", TagType.Double, TagSource.Static, Invariant(spec.VesselVolumeL),
            roundDigits: 0, description: "Паспортный объем реактора в литрах.");
        yield return CreateTag(spec, "target-temperature", "Целевая температура", "TargetTemperatureC", TagType.Double, TagSource.Static, Invariant(spec.TargetTemperatureC),
            roundDigits: 1, description: "Целевая температура рецепта, используемая скриптами отклонения и энергопотребления.");
        yield return CreateTag(spec, "target-ph", "Целевой pH", "TargetPh", TagType.Double, TagSource.Static, Invariant(spec.TargetPh),
            roundDigits: 2, description: "Целевой pH рецепта, используемый inline-скриптом оценки pH.");

        yield return CreateTag(spec, "batch-phase", "Фаза партии", "BatchPhase", TagType.String, TagSource.Scenario, "Setup",
            scenario: CreateBatchPhaseScenario(), specialParameter: SpecialParameter.WorkMode,
            description: "Сценарий фаз batch-процесса: подготовка, заполнение, нагрев, реакция, слив и CIP.");
        yield return CreateTag(spec, "phase-step", "Шаг фазы", "PhaseStep", TagType.Int, TagSource.Scenario, "0",
            scenario: CreatePhaseStepScenario(), description: "Числовой шаг фазы рецепта для внешних систем.");
        yield return CreateTag(spec, "level", "Уровень", "LevelPct", TagType.Double, TagSource.Scenario, "0",
            scenario: CreateBatchLevelScenario(), roundDigits: 1, description: "Уровень реактора, который читают скрипты прогресса и аварий.");
        yield return CreateTag(spec, "temperature", "Температура", "TemperatureC", TagType.Double, TagSource.Scenario, "24",
            scenario: CreateBatchTemperatureScenario(spec), roundDigits: 1, description: "Фактическая температура партии по сценарию нагрева и реакции.");
        yield return CreateTag(spec, "pressure", "Давление", "PressureBar", TagType.Double, TagSource.Generator, Invariant(spec.PressureBaseBar),
            calc: BatchSinusoidCalc(spec.PressureBaseBar, 0.18, 42, distortion: 0.7), roundDigits: 2, description: "Давление в реакторе с небольшой волной регулирования.");
        yield return CreateTag(spec, "agitator-speed", "Скорость мешалки", "AgitatorSpeedRpm", TagType.Double, TagSource.Scenario, "0",
            scenario: CreateAgitatorSpeedScenario(spec), roundDigits: 0, description: "Скорость мешалки, используемая inline-скриптом энергопотребления.");
        yield return CreateTag(spec, "feed-valve-open", "Клапан подачи", "FeedValveOpen", TagType.Bool, TagSource.Scenario, "false",
            scenario: CreateFeedValveScenario(), description: "Состояние клапана подачи сырья.");
        yield return CreateTag(spec, "drain-valve-open", "Клапан слива", "DrainValveOpen", TagType.Bool, TagSource.Scenario, "false",
            scenario: CreateDrainValveScenario(), description: "Состояние клапана слива продукта или моющего раствора.");
        yield return CreateTag(spec, "cip-active", "CIP активен", "CipActive", TagType.Bool, TagSource.Scenario, "false",
            scenario: CreateCipActiveScenario(), description: "Флаг активной промывки, используемый скриптами качества и аварий.");

        yield return CreateTag(spec, "residence-time", "Время реакции", "ResidenceTimeMin", TagType.Double, TagSource.Formula, "0",
            formula: ScriptFormula(BatchReactorScriptId(spec, "residence-time")), roundDigits: 1,
            description: "Накопленное время нахождения партии в фазе Reaction с persistent state скрипта.");
        yield return CreateTag(spec, "batch-progress", "Прогресс партии", "BatchProgressPct", TagType.Double, TagSource.FormulaScript, "0",
            calc: BatchLineCalc(0, 100, TotalBatchDurationSec), formula: ScriptFormula(BatchReactorScriptId(spec, "batch-progress")), roundDigits: 1,
            description: "Линейный генератор прогресса с CSX-постобработкой по фазе, уровню и времени реакции.");
        yield return CreateTag(spec, "temperature-deviation", "Отклонение температуры", "TemperatureDeviationC", TagType.Double, TagSource.Script, "0",
            formula: ScriptFormula(BatchReactorScriptId(spec, "temperature-deviation")), roundDigits: 1,
            description: "Отклонение температуры от статичной рецептурной уставки.");
        yield return CreateTag(spec, "ph-estimate", "Оценка pH", "PhEstimate", TagType.Double, TagSource.Script, Invariant(spec.TargetPh),
            formula: InlineFormula(PhEstimateInlineScript), roundDigits: 2,
            description: "Inline-скрипт оценивает pH по фазе, температуре, уровню и времени реакции.");
        yield return CreateTag(spec, "quality-state", "Качество", "QualityState", TagType.String, TagSource.Script, "Pending",
            formula: ScriptFormula(BatchReactorScriptId(spec, "quality-state")), specialParameter: SpecialParameter.Message,
            description: "Состояние качества партии по рецепту, сырью, температуре, pH и CIP.");
        yield return CreateTag(spec, "energy", "Энергия", "EnergyKw", TagType.Double, TagSource.FormulaScript, "0",
            calc: BatchSinusoidCalc(4.2, 0.6, 55, distortion: 1.0), formula: InlineFormula(EnergyInlineScript), roundDigits: 1,
            description: "Inline formula-script рассчитывает потребление энергии по мешалке, фазе и температуре.");
        yield return CreateTag(spec, "alarm-code", "Код аварии", "AlarmCode", TagType.Int, TagSource.Script, "0",
            formula: ScriptFormula(BatchReactorScriptId(spec, "alarm-code")), specialParameter: SpecialParameter.ErrorNum,
            description: "Код аварии batch-процесса по конфликтам клапанов, CIP, давлению и температуре.");
        yield return CreateTag(spec, "alarm-text", "Текст аварии", "AlarmText", TagType.String, TagSource.Script, string.Empty,
            formula: ScriptFormula(BatchReactorScriptId(spec, "alarm-text")), specialParameter: SpecialParameter.ErrorText,
            description: "Текст аварии по рассчитанному AlarmCode.");
    }

    /// <summary>
    /// Создает сценарий фаз batch-процесса.
    /// </summary>
    /// <returns>Сценарий фаз партии.</returns>
    private static TagScenarioConfigDto CreateBatchPhaseScenario()
    {
        return CreateBatchStaticScenario(
            "Setup",
            ("phase-setup", "Подготовка", 45, "Setup"),
            ("phase-filling", "Заполнение", 150, "Filling"),
            ("phase-heatup", "Нагрев", 240, "HeatUp"),
            ("phase-reaction", "Реакция", 420, "Reaction"),
            ("phase-transfer", "Слив продукта", 120, "Transfer"),
            ("phase-cip", "Промывка CIP", 180, "CIP"));
    }

    /// <summary>
    /// Создает сценарий числового шага фазы.
    /// </summary>
    /// <returns>Сценарий номера шага фазы.</returns>
    private static TagScenarioConfigDto CreatePhaseStepScenario()
    {
        return CreateBatchStaticScenario(
            "0",
            ("step-setup", "Подготовка", 45, "0"),
            ("step-filling", "Заполнение", 150, "10"),
            ("step-heatup", "Нагрев", 240, "20"),
            ("step-reaction", "Реакция", 420, "30"),
            ("step-transfer", "Слив продукта", 120, "40"),
            ("step-cip", "Промывка CIP", 180, "90"));
    }

    /// <summary>
    /// Создает сценарий уровня реактора.
    /// </summary>
    /// <returns>Сценарий уровня batch-реактора.</returns>
    private static TagScenarioConfigDto CreateBatchLevelScenario()
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("level-setup", "Подготовка", 45, "0"),
                LineSegment("level-filling", "Заполнение", 150, 0, 75),
                StaticSegment("level-heatup", "Нагрев", 240, "75"),
                SineSegment("level-reaction", "Реакция", 420, 76, amplitude: 2, period: 90),
                LineSegment("level-transfer", "Слив продукта", 120, 76, 15),
                LineSegment("level-cip", "Промывка CIP", 180, 15, 0),
            ],
            ContinueOnFormulaEnd.Repeat,
            "0");
    }

    /// <summary>
    /// Создает сценарий температуры batch-партии.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <returns>Сценарий температуры партии.</returns>
    private static TagScenarioConfigDto CreateBatchTemperatureScenario(BatchReactorSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("temperature-setup", "Подготовка", 45, "24"),
                LineSegment("temperature-filling", "Заполнение", 150, 24, 32),
                CurveSegment("temperature-heatup", "Нагрев", 240, 32, spec.TargetTemperatureC, curvature: 0.35),
                SineSegment("temperature-reaction", "Реакция", 420, spec.TargetTemperatureC, amplitude: 1.8, period: 105),
                LineSegment("temperature-transfer", "Слив продукта", 120, spec.TargetTemperatureC, 48),
                LineSegment("temperature-cip", "Промывка CIP", 180, 48, 32),
            ],
            ContinueOnFormulaEnd.Repeat,
            "24");
    }

    /// <summary>
    /// Создает сценарий скорости мешалки.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <returns>Сценарий скорости мешалки.</returns>
    private static TagScenarioConfigDto CreateAgitatorSpeedScenario(BatchReactorSeedSpec spec)
    {
        return new TagScenarioConfigDto(
            [
                StaticSegment("agitator-setup", "Подготовка", 45, "0"),
                LineSegment("agitator-filling", "Заполнение", 150, 0, 180),
                LineSegment("agitator-heatup", "Нагрев", 240, 180, spec.ReactionSpeedRpm),
                SineSegment("agitator-reaction", "Реакция", 420, spec.ReactionSpeedRpm, amplitude: 25, period: 75),
                LineSegment("agitator-transfer", "Слив продукта", 120, spec.ReactionSpeedRpm, 220),
                LineSegment("agitator-cip", "Промывка CIP", 180, 220, 80),
            ],
            ContinueOnFormulaEnd.Repeat,
            "0");
    }

    /// <summary>
    /// Создает сценарий клапана подачи сырья.
    /// </summary>
    /// <returns>Сценарий клапана подачи.</returns>
    private static TagScenarioConfigDto CreateFeedValveScenario()
    {
        return CreateBatchStaticScenario(
            "false",
            ("feed-setup", "Подготовка", 45, "false"),
            ("feed-filling", "Заполнение", 150, "true"),
            ("feed-heatup", "Нагрев", 240, "false"),
            ("feed-reaction", "Реакция", 420, "false"),
            ("feed-transfer", "Слив продукта", 120, "false"),
            ("feed-cip", "Промывка CIP", 180, "false"));
    }

    /// <summary>
    /// Создает сценарий клапана слива.
    /// </summary>
    /// <returns>Сценарий клапана слива.</returns>
    private static TagScenarioConfigDto CreateDrainValveScenario()
    {
        return CreateBatchStaticScenario(
            "false",
            ("drain-setup", "Подготовка", 45, "false"),
            ("drain-filling", "Заполнение", 150, "false"),
            ("drain-heatup", "Нагрев", 240, "false"),
            ("drain-reaction", "Реакция", 420, "false"),
            ("drain-transfer", "Слив продукта", 120, "true"),
            ("drain-cip", "Промывка CIP", 180, "true"));
    }

    /// <summary>
    /// Создает сценарий CIP-промывки.
    /// </summary>
    /// <returns>Сценарий активности CIP.</returns>
    private static TagScenarioConfigDto CreateCipActiveScenario()
    {
        return CreateBatchStaticScenario(
            "false",
            ("cip-setup", "Подготовка", 45, "false"),
            ("cip-filling", "Заполнение", 150, "false"),
            ("cip-heatup", "Нагрев", 240, "false"),
            ("cip-reaction", "Реакция", 420, "false"),
            ("cip-transfer", "Слив продукта", 120, "false"),
            ("cip-active", "Промывка CIP", 180, "true"));
    }

    /// <summary>
    /// Создает статический сценарий batch-реактора.
    /// </summary>
    /// <param name="startValue">Начальное значение сценария.</param>
    /// <param name="segments">Участки сценария.</param>
    /// <returns>Конфигурация сценария.</returns>
    private static TagScenarioConfigDto CreateBatchStaticScenario(
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
    /// Создает линейный генератор для formula-script тега batch-реактора.
    /// </summary>
    /// <param name="start">Начальное значение.</param>
    /// <param name="finish">Конечное значение.</param>
    /// <param name="duration">Длительность генерации.</param>
    /// <returns>Конфигурация линейного генератора.</returns>
    private static TagCalcConfigDto BatchLineCalc(double start, double finish, int duration)
    {
        return new TagCalcConfigDto(
            CalcType.Line,
            Start: Invariant(start),
            Finish: Invariant(finish),
            Duration: duration,
            Amplitude: null,
            Period: null,
            Curvature: null,
            Distortion: null);
    }

    /// <summary>
    /// Создает синусоидальный генератор для batch-реактора.
    /// </summary>
    /// <param name="start">Базовое значение.</param>
    /// <param name="amplitude">Амплитуда колебания.</param>
    /// <param name="period">Период колебания.</param>
    /// <param name="distortion">Искажение сигнала.</param>
    /// <returns>Конфигурация синусоидального генератора.</returns>
    private static TagCalcConfigDto BatchSinusoidCalc(double start, double amplitude, int period, double distortion)
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
    /// Возвращает идентификатор scoped-скрипта batch-реактора.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <param name="scriptName">Короткое имя скрипта без расширения.</param>
    /// <returns>Идентификатор скрипта.</returns>
    private static string BatchReactorScriptId(BatchReactorSeedSpec spec, string scriptName)
    {
        return $"scr-{spec.Id["em-".Length..]}-{scriptName}";
    }

    /// <summary>
    /// Создает CSX-скрипты, привязанные к batch-реактору.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция scoped-скриптов batch-реактора.</returns>
    private static IEnumerable<ScriptFileEntity> CreateBatchReactorScripts(BatchReactorSeedSpec spec, DateTimeOffset now)
    {
        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "residence-time"),
            "residence-time.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var phase = ReadText("BatchPhase", "Setup");
            var residenceMinutes = UniEmu.State.Get<double>("residenceMinutes", 0);

            if (phase == "Reaction")
            {
                var previous = UniEmu.State.PrevTimestamp;
                if (previous is not null)
                    residenceMinutes += Math.Max(0, (Now - previous.Value).TotalMinutes);
            }
            else if (phase == "Setup" || phase == "Filling")
            {
                residenceMinutes = 0;
            }

            UniEmu.State.Set("residenceMinutes", residenceMinutes);
            return Math.Round(residenceMinutes, 1);
            """,
            now.AddHours(-1).AddMinutes(20));

        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "batch-progress"),
            "batch-progress.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var baseProgress = ToDouble(UniEmu.Tag.Value, 0);
            var phase = ReadText("BatchPhase", "Setup");
            var level = ReadNumber("LevelPct", 0);
            var residence = ReadNumber("ResidenceTimeMin", 0);

            var progress = phase switch
            {
                "Setup" => Clamp(baseProgress * 0.05, 0, 5),
                "Filling" => Clamp(5 + level * 0.25, 5, 28),
                "HeatUp" => Clamp(30 + baseProgress * 0.25, 30, 55),
                "Reaction" => Clamp(55 + residence * 0.08, 55, 88),
                "Transfer" => Clamp(90 + (75 - level) * 0.1, 88, 98),
                "CIP" => 100,
                _ => baseProgress,
            };

            return Math.Round(Clamp(progress, 0, 100), 1);
            """,
            now.AddHours(-1).AddMinutes(21));

        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "temperature-deviation"),
            "temperature-deviation.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var temperature = ReadNumber("TemperatureC", 24);
            var target = ReadNumber("TargetTemperatureC", 72);

            return Math.Round(temperature - target, 1);
            """,
            now.AddHours(-1).AddMinutes(22));

        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "quality-state"),
            "quality-state.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var materialLot = ReadText("MaterialLot", string.Empty);
            if (string.IsNullOrWhiteSpace(materialLot))
                return "Hold: material lot missing";

            var phase = ReadText("BatchPhase", "Setup");
            if (phase == "CIP")
                return "Cleaning";

            var deviation = Math.Abs(ReadNumber("TemperatureDeviationC", 0));
            if (phase is "HeatUp" or "Reaction" && deviation > 5)
                return "Hold: temperature deviation";

            var ph = ReadNumber("PhEstimate", 7);
            var targetPh = ReadNumber("TargetPh", 6.8);
            if (phase == "Reaction" && Math.Abs(ph - targetPh) > 0.35)
                return "Hold: pH correction";

            var progress = ReadNumber("BatchProgressPct", 0);
            if (phase == "Transfer" && progress > 95)
                return "Released";

            return phase == "Reaction"
                ? "InSpec"
                : "Pending";
            """,
            now.AddHours(-1).AddMinutes(23));

        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "alarm-code"),
            "alarm-code.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var phase = ReadText("BatchPhase", "Setup");
            var feedOpen = ReadBool("FeedValveOpen", false);
            var drainOpen = ReadBool("DrainValveOpen", false);
            var cipActive = ReadBool("CipActive", false);
            var pressure = ReadNumber("PressureBar", 1.2);
            var deviation = Math.Abs(ReadNumber("TemperatureDeviationC", 0));

            if (feedOpen && drainOpen)
                return 610;

            if (pressure > 4.2)
                return 620;

            if (phase == "Reaction" && deviation > 8)
                return 630;

            if (phase != "CIP" && cipActive)
                return 640;

            return 0;
            """,
            now.AddHours(-1).AddMinutes(24));

        yield return CreateEmulatorScript(
            BatchReactorScriptId(spec, "alarm-text"),
            "alarm-text.csx",
            spec.Id,
            """
            #load "read-tags.csx"

            var code = (int)Math.Round(ReadNumber("AlarmCode", 0));
            return code switch
            {
                610 => "Feed and drain valves are open at the same time.",
                620 => "Reactor pressure is above the process limit.",
                630 => "Reaction temperature deviation is above tolerance.",
                640 => "CIP is active outside the cleaning phase.",
                _ => string.Empty,
            };
            """,
            now.AddHours(-1).AddMinutes(25));
    }

    /// <summary>
    /// Создает тег batch-реактора через общую фабрику тегов.
    /// </summary>
    /// <param name="spec">Настройки демонстрационного batch-реактора.</param>
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
        BatchReactorSeedSpec spec,
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

    private const int TotalBatchDurationSec = 1155;

    private const string PhEstimateInlineScript =
        """
        #load "read-tags.csx"

        var phase = ReadText("BatchPhase", "Setup");
        var targetPh = ReadNumber("TargetPh", 6.8);
        var targetTemperature = ReadNumber("TargetTemperatureC", 72);
        var temperature = ReadNumber("TemperatureC", 24);
        var residence = ReadNumber("ResidenceTimeMin", 0);
        var level = ReadNumber("LevelPct", 0);

        var phaseOffset = phase switch
        {
            "Filling" => 0.25,
            "HeatUp" => 0.12,
            "Reaction" => -Math.Min(0.18, residence * 0.002),
            "Transfer" => -0.05,
            "CIP" => 0.45,
            _ => 0.3,
        };
        var thermalOffset = (temperature - targetTemperature) * 0.003;
        var dilutionOffset = level < 20 ? 0.08 : 0;

        return Math.Round(Clamp(targetPh + phaseOffset + thermalOffset + dilutionOffset, 5.8, 8.4), 2);
        """;

    private const string EnergyInlineScript =
        """
        #load "read-tags.csx"

        var baseKw = ToDouble(UniEmu.Tag.Value, 4.2);
        var phase = ReadText("BatchPhase", "Setup");
        var speed = ReadNumber("AgitatorSpeedRpm", 0);
        var temperature = ReadNumber("TemperatureC", 24);
        var targetTemperature = ReadNumber("TargetTemperatureC", 72);

        var heaterKw = phase is "HeatUp" or "Reaction"
            ? Math.Max(0, targetTemperature - temperature) * 0.8
            : 0;
        var agitatorKw = speed * 0.015;

        return Math.Round(Clamp(baseKw + heaterKw + agitatorKw, 0, 80), 1);
        """;

    /// <summary>
    /// Настройки демонстрационного batch-реактора.
    /// </summary>
    /// <param name="Id">Идентификатор эмулятора.</param>
    /// <param name="Name">Имя эмулятора.</param>
    /// <param name="ProtocolId">Идентификатор протокола Dispatcher.</param>
    /// <param name="IntervalSec">Интервал публикации телеметрии.</param>
    /// <param name="TotalRequests">Начальный счетчик публикаций.</param>
    /// <param name="UnitName">Технологическое имя реактора.</param>
    /// <param name="Area">Производственный участок.</param>
    /// <param name="Line">Производственная линия.</param>
    /// <param name="EquipmentModel">Паспортная модель оборудования.</param>
    /// <param name="ControllerVersion">Версия проекта контроллера.</param>
    /// <param name="RecipeName">Имя активного рецепта.</param>
    /// <param name="ProductCode">Код продукта.</param>
    /// <param name="BatchId">Идентификатор партии.</param>
    /// <param name="MaterialLot">Партия сырья.</param>
    /// <param name="OperatorId">Идентификатор оператора.</param>
    /// <param name="VesselVolumeL">Паспортный объем реактора.</param>
    /// <param name="TargetTemperatureC">Целевая температура рецепта.</param>
    /// <param name="TargetPh">Целевой pH рецепта.</param>
    /// <param name="ReactionSpeedRpm">Базовая скорость мешалки в реакции.</param>
    /// <param name="PressureBaseBar">Базовое давление реактора.</param>
    private sealed record BatchReactorSeedSpec(
        string Id,
        string Name,
        int ProtocolId,
        int IntervalSec,
        long TotalRequests,
        string UnitName,
        string Area,
        string Line,
        string EquipmentModel,
        string ControllerVersion,
        string RecipeName,
        string ProductCode,
        string BatchId,
        string MaterialLot,
        string OperatorId,
        double VesselVolumeL,
        double TargetTemperatureC,
        double TargetPh,
        double ReactionSpeedRpm,
        double PressureBaseBar);
}
