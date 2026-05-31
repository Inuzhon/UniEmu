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
public static partial class UniEmuSeeder
{
    private const int DEFAULT_INTERVAL = 5;

    /// <summary>
    /// Заполняет пустую базу промышленными эмуляторами, тегами, сценариями, генераторами, скриптами и событиями.
    /// </summary>
    /// <param name="db">Контекст базы данных UniEmu.</param>
    /// <param name="defaultTargetUrl">URL целевой системы для создаваемых seed-эмуляторов.</param>
    /// <param name="cancellationToken">Токен отмены операции сохранения.</param>
    /// <returns>Задача инициализации seed-данных.</returns>
    public static async Task SeedAsync(
        UniEmuDbContext db,
        string? defaultTargetUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (db.Emulators.Any())
            return;

        var now = DateTimeOffset.UtcNow;
        var targetUrl = string.IsNullOrWhiteSpace(defaultTargetUrl)
            ? "http://127.0.0.1:8080"
            : defaultTargetUrl.Trim();
        var furnaces = CreateFurnaceSpecs();
        var cncMachines = CreateCncSpecs();
        var batchReactors = CreateBatchReactorSpecs();
        var legacyOvens = CreateLegacyOvenSpecs();

        db.Emulators.AddRange(
            furnaces.Select(spec => CreateFurnaceEmulator(spec, now, targetUrl))
                .Concat(cncMachines.Select(spec => CreateCncEmulator(spec, now, targetUrl)))
                .Concat(batchReactors.Select(spec => CreateBatchReactorEmulator(spec, now, targetUrl)))
                .Concat(legacyOvens.Select(spec => CreateLegacyOvenEmulator(spec, now, targetUrl))));
        db.EmulatorTags.AddRange(
            furnaces.SelectMany(CreateFurnaceTags)
                .Concat(cncMachines.SelectMany(CreateCncTags))
                .Concat(batchReactors.SelectMany(CreateBatchReactorTags))
                .Concat(legacyOvens.SelectMany(CreateLegacyOvenTags)));
        db.ScriptFiles.AddRange(
            CreateSharedScripts(now)
                .Concat(furnaces.SelectMany(spec => CreateFurnaceScripts(spec, now)))
                .Concat(cncMachines.SelectMany(spec => CreateCncScripts(spec, now)))
                .Concat(batchReactors.SelectMany(spec => CreateBatchReactorScripts(spec, now)))
                .Concat(legacyOvens.SelectMany(spec => CreateLegacyOvenScripts(spec, now))));
        db.CncPrograms.AddRange(CreateCncPrograms(cncMachines, now));
        //db.SystemEvents.AddRange(
        //    CreateFurnaceSeedEvents(furnaces, now)
        //        .Concat(CreateCncSeedEvents(cncMachines, now)));

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Создает общие CSX-скрипты, доступные всем эмуляторам.
    /// </summary>
    /// <param name="now">Текущее время заполнения базы.</param>
    /// <returns>Коллекция общих скриптов.</returns>
    private static IEnumerable<ScriptFileEntity> CreateSharedScripts(DateTimeOffset now)
    {
        yield return CreateSharedScript(
            "scr-math",
            "math.csx",
            """
            /// <summary>
            /// Ограничивает число заданным минимальным и максимальным значением.
            /// </summary>
            double Clamp(double value, double min, double max)
            {
                if (value < min)
                    return min;

                if (value > max)
                    return max;

                return value;
            }

            /// <summary>
            /// Преобразует значение тега в число double или возвращает запасное значение.
            /// </summary>
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

            /// <summary>
            /// Преобразует значение тега в строку или возвращает запасное значение.
            /// </summary>
            string ToText(object? value, string fallback)
            {
                return value?.ToString() ?? fallback;
            }

            /// <summary>
            /// Преобразует значение тега в bool или возвращает запасное значение.
            /// </summary>
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
            now.AddHours(-2));

        yield return CreateSharedScript(
            "scr-read-tags",
            "read-tags.csx",
            """
            #load "math.csx"

            /// <summary>
            /// Читает числовой тег по ключу или возвращает запасное значение.
            /// </summary>
            double ReadNumber(string key, double fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToDouble(tag?.Value, fallback)
                    : fallback;
            }

            /// <summary>
            /// Читает строковый тег по ключу или возвращает запасное значение.
            /// </summary>
            string ReadText(string key, string fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToText(tag?.Value, fallback)
                    : fallback;
            }

            /// <summary>
            /// Читает логический тег по ключу или возвращает запасное значение.
            /// </summary>
            bool ReadBool(string key, bool fallback)
            {
                return UniEmu.Tags.TryGetValue(key, out var tag)
                    ? ToBool(tag?.Value, fallback)
                    : fallback;
            }
            """,
            now.AddHours(-2).AddMinutes(1));
    }

    /// <summary>
    /// Создает тег эмулятора и сериализует связанные настройки в JSON.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора-владельца.</param>
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
        string emulatorId,
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
            Id = $"tg-{emulatorId["em-".Length..]}-{idSuffix}",
            EmulatorId = emulatorId,
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
            SpecialParameter = UniEmuJson.EnumString(FixSpecialParameter(specialParameter)),
            Description = description,
        };
    }

    private static SpecialParameter FixSpecialParameter(SpecialParameter? specialParameter)
    {
        return specialParameter switch
        {
            SpecialParameter.PrgName => specialParameter.Value,
            //SpecialParameter.PartCounter => expr,
            //SpecialParameter.ErrorNum => expr,
            //SpecialParameter.FeedOvr => expr,
            //SpecialParameter.SpindleOvr => expr,
            //SpecialParameter.JogOvr => expr,
            SpecialParameter.FrameNum => specialParameter.Value,
            SpecialParameter.FrameText => specialParameter.Value,
            //SpecialParameter.ToolNum => expr,
            //SpecialParameter.WorkMode => expr,
            //SpecialParameter.SystemState => expr,
            //SpecialParameter.MachineReadiness => expr,
            //SpecialParameter.TechnologicalStop => expr,
            //SpecialParameter.EmergencyStop => expr,
            //SpecialParameter.FeedRate => expr,
            //SpecialParameter.ErrorText => expr,
            //SpecialParameter.CycleTime => expr,
            //SpecialParameter.SpindleSpeed => expr,
            //SpecialParameter.SpindleLoad => expr,
            //SpecialParameter.AxisLoad => expr,
            //SpecialParameter.AxisPosition => expr,
            //SpecialParameter.Message => expr,
            //SpecialParameter.CNCModel => expr,
            //SpecialParameter.FirmwareVersion => expr,
            //SpecialParameter.SerialNumber => expr,
            //SpecialParameter.PLCVersion => expr,
            SpecialParameter.Subprogram => specialParameter.Value,
            _ => SpecialParameter.None,
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
    /// Создает скрипт, доступный только указанному эмулятору.
    /// </summary>
    /// <param name="id">Идентификатор скрипта.</param>
    /// <param name="name">Имя скрипта для редактора и директив <c>#load</c>.</param>
    /// <param name="emulatorId">Идентификатор эмулятора-владельца.</param>
    /// <param name="content">Содержимое CSX-скрипта.</param>
    /// <param name="updatedAt">Время последнего обновления.</param>
    /// <returns>Сущность scoped-скрипта.</returns>
    private static ScriptFileEntity CreateEmulatorScript(
        string id,
        string name,
        string emulatorId,
        string content,
        DateTimeOffset updatedAt)
    {
        return new ScriptFileEntity
        {
            Id = id,
            Name = name,
            Scope = UniEmuJson.EnumString(ScriptScope.Emulator),
            EmulatorId = emulatorId,
            Content = content,
            UpdatedAt = updatedAt,
            SizeBytes = Encoding.UTF8.GetByteCount(content),
        };
    }

    /// <summary>
    /// Создает CNC-программу с корректным размером содержимого.
    /// </summary>
    /// <param name="id">Идентификатор программы.</param>
    /// <param name="name">Имя файла программы.</param>
    /// <param name="emulatorId">Идентификатор эмулятора-владельца.</param>
    /// <param name="description">Описание программы.</param>
    /// <param name="content">Текст G-code программы.</param>
    /// <param name="timestamp">Время загрузки и обновления программы.</param>
    /// <returns>Сущность CNC-программы.</returns>
    private static CncProgramEntity CreateCncProgram(
        string id,
        string name,
        string emulatorId,
        string description,
        string content,
        DateTimeOffset timestamp)
    {
        return new CncProgramEntity
        {
            Id = id,
            Name = name,
            Scope = UniEmuJson.EnumString(CncScope.Emulator),
            EmulatorId = emulatorId,
            Description = description,
            Content = content,
            SizeBytes = Encoding.UTF8.GetByteCount(content),
            UpdatedAt = timestamp,
            UploadedAt = timestamp,
            IsBinary = false,
        };
    }

    /// <summary>
    /// Создает ссылку на сохраненный CSX-скрипт.
    /// </summary>
    /// <param name="scriptId">Идентификатор скрипта.</param>
    /// <returns>Конфигурация скриптовой формулы.</returns>
    private static TagFormulaConfigDto ScriptFormula(string scriptId) => new(scriptId, null);

    /// <summary>
    /// Создает встроенную CSX-формулу без отдельной записи в таблице скриптов.
    /// </summary>
    /// <param name="script">Текст встроенного CSX-скрипта.</param>
    /// <returns>Конфигурация inline-формулы.</returns>
    private static TagFormulaConfigDto InlineFormula(string script) => new(null, script);

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
    /// Создает криволинейный участок сценария.
    /// </summary>
    /// <param name="id">Идентификатор участка.</param>
    /// <param name="label">Отображаемая подпись участка.</param>
    /// <param name="duration">Длительность участка в секундах.</param>
    /// <param name="start">Начальное значение.</param>
    /// <param name="finish">Конечное значение.</param>
    /// <param name="curvature">Коэффициент кривизны.</param>
    /// <returns>Участок сценария.</returns>
    private static TagScenarioSegmentDto CurveSegment(string id, string label, int duration, double start, double finish, double curvature)
    {
        return new TagScenarioSegmentDto(
            id,
            duration,
            new TagCalcConfigDto(
                CalcType.Curve,
                Start: Invariant(start),
                Finish: Invariant(finish),
                Duration: duration,
                Amplitude: null,
                Period: null,
                Curvature: curvature,
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
}
