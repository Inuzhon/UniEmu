using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Runtime;

/// <summary>
/// Периодическая задача публикации телеметрии эмулятора в Dispatcher и локальный мониторинг.
/// </summary>
[DisallowConcurrentExecution]
public sealed class EmulatorPublishJob(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TelemetryValueGenerator valueGenerator,
    TagScriptExecutionService scriptExecutionService,
    TagRuntimeStateStore stateStore,
    TelemetryPacketSender sender,
    RuntimeUpdateService runtimeUpdateService,
    ILogger<EmulatorPublishJob> logger) : IJob
{
    /// <summary>
    /// Формирует очередной снимок значений тегов, сохраняет точку телеметрии и отправляет пакет в Dispatcher.
    /// </summary>
    /// <param name="context">Контекст выполнения Quartz-задачи с идентификатором эмулятора.</param>
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var emulatorId = context.MergedJobDataMap.GetString(RuntimeJobKeys.EmulatorId);
        if (string.IsNullOrWhiteSpace(emulatorId))
        {
            logger.LogWarning("Publish job is missing emulatorId");
            return;
        }

        var emulator = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (emulator is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.ClearEmulator(emulatorId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var scheduledAt = context.ScheduledFireTimeUtc ?? now;
        emulator.LastError = null;
        var generatedValues = await BuildValuesAsync(emulator, now, scheduledAt, cancellationToken);
        var telemetryValues = BuildTelemetryValues(generatedValues);

        var dispatcherValues = BuildDispatcherValues(emulator.Tags, generatedValues);
        var machineIntegrationId = GetMachineIntegrationId(emulator);
        var mainProgram = await ResolveProgramAsync(emulator.Id, generatedValues, SpecialParameter.PrgName, cancellationToken);
        var subProgram = await ResolveProgramAsync(emulator.Id, generatedValues, SpecialParameter.Subprogram, cancellationToken);

        var level = EventLevel.Success;
        var message = string.Empty;

        var telemetryPoint = new TelemetryPointEntity
        {
            EmulatorId = emulator.Id,
            Timestamp = now,
            ValuesJson = UniEmuJson.Serialize(telemetryValues),
        };
        db.TelemetryPoints.Add(telemetryPoint);

        try
        {
            var request = new UniversalPostRequest(machineIntegrationId, UseInnerId: true, dispatcherValues);
            var answer = await sender.SendMonitoringAsync(emulator.TargetUrl, request, cancellationToken);
            await HandleDispatcherAnswerAsync(emulator, machineIntegrationId, mainProgram, subProgram, answer, cancellationToken);
            emulator.TotalRequests++;
        }
        catch (Exception ex)
        {
            level = EventLevel.Error;
            message = $"Ошибка отправки телеметрии: {ex.Message}";
            emulator.LastError = ex.Message;
            logger.LogWarning(ex, "Telemetry send failed for emulator {EmulatorId}", emulator.Id);
        }

        emulator.LastRun = now;
        emulator.NextRun = now.AddSeconds(Math.Max(1, emulator.IntervalSec));
        emulator.StartedAt ??= now;

        var systemEvent = default(SystemEventEntity);
        if (level == EventLevel.Error)
        {
            systemEvent = new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(level),
                Message = message,
                Timestamp = now,
            };
            db.SystemEvents.Add(systemEvent);
        }

        await db.SaveChangesAsync(cancellationToken);

        await runtimeUpdateService.PublishTelemetryAsync(emulator.Id, telemetryPoint.ToDto(), cancellationToken);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(emulator.ToDto(emulator.Tags.Count), cancellationToken);

        if (systemEvent is not null)
            await runtimeUpdateService.PublishEventCreatedAsync(systemEvent.ToDto(), cancellationToken);
    }

    /// <summary>
    /// Собирает согласованный список значений тегов для одной точки публикации.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого строится снимок значений.</param>
    /// <param name="timestamp">Фактическое время формирования точки телеметрии.</param>
    /// <param name="scheduledAt">Плановое время срабатывания задачи публикации.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Значения тегов в порядке конфигурации эмулятора.</returns>
    private async Task<IReadOnlyList<GeneratedTagValue>> BuildValuesAsync(
        EmulatorEntity emulator,
        DateTimeOffset timestamp,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        var valuesByTagId = new Dictionary<string, GeneratedTagValue>(StringComparer.Ordinal);

        foreach (var tag in emulator.Tags.Where(tag => !IsScriptBackedTag(tag)))
        {
            valuesByTagId[tag.Id] = await BuildTagValueAsync(emulator, tag, timestamp, scheduledAt, cancellationToken);
        }

        foreach (var tag in emulator.Tags.Where(IsScriptBackedTag))
        {
            valuesByTagId[tag.Id] = await BuildTagValueAsync(emulator, tag, timestamp, scheduledAt, cancellationToken);
        }

        var values = emulator.Tags
            .Select(tag => valuesByTagId[tag.Id])
            .ToList();

        values = await ApplySpecializedProgramValuesAsync(emulator, values, timestamp, cancellationToken);

        return values;
    }

    /// <summary>
    /// Получает значение одного тега из свежего runtime-состояния, сохраненного preview или прямого расчета.
    /// </summary>
    /// <param name="emulator">Эмулятор, которому принадлежит тег.</param>
    /// <param name="tag">Конфигурация вычисляемого тега.</param>
    /// <param name="timestamp">Время, которое передается генераторам и скриптам.</param>
    /// <param name="scheduledAt">Плановое время публикации для проверки свежести runtime-значения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Вычисленное или восстановленное значение тега.</returns>
    private async Task<GeneratedTagValue> BuildTagValueAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        if (ShouldWaitForScheduledValue(emulator, tag))
        {
            var freshValue = await stateStore.WaitForValueAsync(
                emulator.Id,
                tag.Id,
                scheduledAt.AddMilliseconds(-50),
                GetCalculationWaitTimeout(emulator),
                cancellationToken);

            if (freshValue is not null)
            {
                return FromRuntimeValue(tag, freshValue);
            }
        }

        if (stateStore.TryGet(emulator.Id, tag.Id, out var runtimeValue) && ShouldUseStoredValue(emulator, tag, runtimeValue, scheduledAt))
        {
            return FromRuntimeValue(tag, runtimeValue);
        }

        if (ShouldUsePersistedPreview(tag))
        {
            var persisted = FromPersistedPreview(tag);
            stateStore.Set(emulator.Id, tag.Id, tag.Name, persisted.Value, persisted.NumericValue, timestamp);
            return persisted;
        }

        var generated = await GenerateTagAsync(emulator, tag, timestamp, cancellationToken);
        tag.Preview = TelemetryValueGenerator.ToPreview(generated.Value);
        stateStore.Set(emulator.Id, tag.Id, tag.Name, generated.Value, generated.NumericValue, timestamp);

        return generated;
    }

    /// <summary>
    /// Преобразует значения тегов в список параметров, отправляемых в Dispatcher.
    /// </summary>
    /// <param name="tags">Конфигурации тегов эмулятора.</param>
    /// <param name="generatedValues">Согласованный список рассчитанных значений тегов.</param>
    /// <returns>Список включенных тегов в формате Dispatcher-протокола.</returns>
    public static List<UniversalValue> BuildDispatcherValues(
        IReadOnlyList<EmulatorTagEntity> tags,
        IReadOnlyList<GeneratedTagValue> generatedValues)
    {
        return tags
            .Zip(generatedValues)
            .Where(pair => pair.First.Enabled)
            .Select(pair => new UniversalValue(pair.Second.Key, pair.Second.Value))
            .ToList();
    }

    /// <summary>
    /// Преобразует рассчитанные значения тегов в словарь для хранения в точке телеметрии.
    /// </summary>
    /// <param name="generatedValues">Список рассчитанных значений тегов.</param>
    /// <returns>Словарь значений, индексированный отображаемым именем тега.</returns>
    public static IReadOnlyDictionary<string, object?> BuildTelemetryValues(
        IReadOnlyList<GeneratedTagValue> generatedValues)
    {
        return generatedValues
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создает рассчитанное значение тега из актуального runtime-состояния.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <param name="runtimeValue">Последнее значение тега из runtime-хранилища.</param>
    /// <returns>Значение в общем формате publish-пайплайна.</returns>
    private static GeneratedTagValue FromRuntimeValue(EmulatorTagEntity tag, TagRuntimeValue runtimeValue)
    {
        return new GeneratedTagValue(
            tag.Key,
            tag.Name,
            runtimeValue.Value,
            runtimeValue.NumericValue,
            GetSpecialParameter(tag));
    }

    /// <summary>
    /// Восстанавливает значение тега из сохраненного preview с учетом типа тега.
    /// </summary>
    /// <param name="tag">Конфигурация тега с сохраненным preview.</param>
    /// <returns>Значение в общем формате publish-пайплайна.</returns>
    private static GeneratedTagValue FromPersistedPreview(EmulatorTagEntity tag)
    {
        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var value = TelemetryValueGenerator.FromPreview(tagType, tag.Preview);

        return new GeneratedTagValue(
            tag.Key,
            tag.Name,
            value,
            TelemetryValueGenerator.ToNumericValue(value),
            GetSpecialParameter(tag));
    }

    /// <summary>
    /// Вычисляет значение тега через генератор, CSX-скрипт или комбинированную формулу-скрипт.
    /// </summary>
    /// <param name="emulator">Эмулятор, для которого выполняется расчет.</param>
    /// <param name="tag">Конфигурация рассчитываемого тега.</param>
    /// <param name="timestamp">Время расчета значения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Рассчитанное значение тега.</returns>
    private async Task<GeneratedTagValue> GenerateTagAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        if (source == TagSource.FormulaScript)
        {
            var generated = valueGenerator.GenerateTag(emulator, tag, timestamp);
            try
            {
                return await scriptExecutionService.GenerateScriptTagAsync(
                    emulator,
                    tag,
                    timestamp,
                    cancellationToken,
                    generated.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Formula script tag generation failed for tag {TagId}", tag.Id);
                var message = $"Ошибка вычисления формулы-скрипта тега {tag.Name}: {ex.Message}";
                emulator.LastError = message;
                db.SystemEvents.Add(new SystemEventEntity
                {
                    Id = $"ev-{Guid.NewGuid():N}"[..12],
                    EmulatorId = emulator.Id,
                    EmulatorName = emulator.Name,
                    Level = UniEmuJson.EnumString(EventLevel.Error),
                    Message = message,
                    Timestamp = timestamp,
                });

                return generated;
            }
        }

        if (source is not (TagSource.Script or TagSource.Formula))
        {
            return valueGenerator.GenerateTag(emulator, tag, timestamp);
        }

        try
        {
            return await scriptExecutionService.GenerateScriptTagAsync(emulator, tag, timestamp, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Script tag generation failed for tag {TagId}", tag.Id);
            var message = $"Ошибка вычисления скрипта тега {tag.Name}: {ex.Message}";
            emulator.LastError = message;
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = message,
                Timestamp = timestamp,
            });

            return valueGenerator.GenerateTag(emulator, tag, timestamp);
        }
    }

    /// <summary>
    /// Дополняет снимок значениями номера и текста кадра программы, если такие специальные теги настроены.
    /// </summary>
    /// <param name="emulator">Эмулятор с конфигурацией тегов.</param>
    /// <param name="values">Текущий список рассчитанных значений.</param>
    /// <param name="timestamp">Время, по которому выбирается текущий кадр программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список значений с подставленными специальными тегами кадра.</returns>
    private async Task<List<GeneratedTagValue>> ApplySpecializedProgramValuesAsync(
        EmulatorEntity emulator,
        List<GeneratedTagValue> values,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (!emulator.Tags.Any(IsCalculatedProgramFrameTag))
        {
            return values;
        }

        var programs = await dataCache.GetVisibleCncProgramsAsync(emulator.Id, cancellationToken);
        var mainProgram = ResolveProgram(programs, emulator.Id, GetSpecialStringValue(values, SpecialParameter.PrgName));
        var subProgram = ResolveProgram(programs, emulator.Id, GetSpecialStringValue(values, SpecialParameter.Subprogram));
        var frame = CalculateProgramFrame(emulator, timestamp, subProgram ?? mainProgram);
        var enriched = values.ToList();

        for (var i = 0; i < emulator.Tags.Count && i < enriched.Count; i++)
        {
            var tag = emulator.Tags[i];
            if (!IsCalculatedProgramFrameTag(tag))
            {
                continue;
            }

            var specialParameter = GetSpecialParameter(tag);
            object value = specialParameter == SpecialParameter.FrameNum
                ? frame.Number
                : frame.Text;

            var generated = new GeneratedTagValue(
                enriched[i].Key,
                enriched[i].Name,
                value,
                TelemetryValueGenerator.ToNumericValue(value),
                specialParameter);

            enriched[i] = generated;
            tag.Preview = TelemetryValueGenerator.ToPreview(value);
            stateStore.Set(emulator.Id, tag.Id, tag.Name, generated.Value, generated.NumericValue, timestamp);
        }

        return enriched;
    }

    /// <summary>
    /// Определяет, можно ли использовать ранее сохраненное runtime-значение вместо расчета в текущей публикации.
    /// </summary>
    /// <param name="emulator">Эмулятор, задающий интервал публикации.</param>
    /// <param name="tag">Конфигурация тега.</param>
    /// <param name="runtimeValue">Последнее сохраненное runtime-значение тега.</param>
    /// <param name="scheduledAt">Плановое время текущей публикации.</param>
    /// <returns><see langword="true"/>, если значение должно браться из runtime-хранилища.</returns>
    private static bool ShouldUseStoredValue(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        TagRuntimeValue runtimeValue,
        DateTimeOffset scheduledAt)
    {
        var trigger = GetTrigger(tag);

        if (trigger.Mode != TagTriggerMode.Interval)
        {
            return true;
        }

        var tagInterval = ToTimeSpan(trigger);
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        if (Math.Abs((tagInterval - publishInterval).TotalMilliseconds) <= 0.5)
        {
            return false;
        }

        var age = scheduledAt - runtimeValue.Timestamp;
        return age <= tagInterval.Add(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    /// Определяет, нужно ли дождаться свежего значения тега, рассчитанного отдельной tag-задачей.
    /// </summary>
    /// <param name="emulator">Эмулятор, задающий интервал публикации.</param>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns><see langword="true"/>, если publish-задача должна подождать свежий runtime-снимок тега.</returns>
    private static bool ShouldWaitForScheduledValue(EmulatorEntity emulator, EmulatorTagEntity tag)
    {
        if (IsCalculatedProgramFrameTag(tag))
        {
            return false;
        }

        if (ShouldCalculateScriptAtPublish(emulator, tag))
        {
            return false;
        }

        var trigger = GetTrigger(tag);

        if (trigger.Mode != TagTriggerMode.Interval)
        {
            return false;
        }

        var tagInterval = ToTimeSpan(trigger);
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        return Math.Abs((tagInterval - publishInterval).TotalMilliseconds) <= 0.5;
    }

    /// <summary>
    /// Определяет скриптовые теги, которые нужно пересчитать внутри publish-задачи после базовых зависимостей.
    /// </summary>
    /// <param name="emulator">Эмулятор, задающий интервал публикации.</param>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns><see langword="true"/>, если скрипт должен рассчитываться в текущем снимке публикации.</returns>
    private static bool ShouldCalculateScriptAtPublish(EmulatorEntity emulator, EmulatorTagEntity tag)
    {
        if (!IsScriptBackedTag(tag))
        {
            return false;
        }

        var trigger = GetTrigger(tag);
        if (trigger.Mode != TagTriggerMode.Interval)
        {
            return false;
        }

        var tagInterval = ToTimeSpan(trigger);
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        return Math.Abs((tagInterval - publishInterval).TotalMilliseconds) <= 0.5;
    }

    /// <summary>
    /// Проверяет, выполняется ли тег через CSX-скрипт или формулу, зависящую от снимка других тегов.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns><see langword="true"/>, если тег нужно рассчитывать после базовых источников.</returns>
    private static bool IsScriptBackedTag(EmulatorTagEntity tag)
    {
        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source is TagSource.Script or TagSource.Formula or TagSource.FormulaScript;
    }

    /// <summary>
    /// Определяет, можно ли использовать сохраненное preview для тегов, которые не рассчитываются на каждой публикации.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns><see langword="true"/>, если значение нужно восстановить из preview.</returns>
    private static bool ShouldUsePersistedPreview(EmulatorTagEntity tag)
    {
        var trigger = GetTrigger(tag);
        return trigger.Mode is TagTriggerMode.Once or TagTriggerMode.Cron;
    }

    /// <summary>
    /// Возвращает триггер тега или безопасный триггер по умолчанию.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns>Десериализованная конфигурация запуска тега.</returns>
    private static TagTriggerDto GetTrigger(EmulatorTagEntity tag)
    {
        return UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
               ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);
    }

    /// <summary>
    /// Рассчитывает максимальное время ожидания tag-задачи перед самостоятельным расчетом значения.
    /// </summary>
    /// <param name="emulator">Эмулятор, задающий интервал публикации.</param>
    /// <returns>Таймаут ожидания свежего runtime-значения.</returns>
    private static TimeSpan GetCalculationWaitTimeout(EmulatorEntity emulator)
    {
        var publishInterval = TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec));
        var timeoutMs = Math.Clamp(publishInterval.TotalMilliseconds * 0.75, 50, 5000);
        return TimeSpan.FromMilliseconds(timeoutMs);
    }

    /// <summary>
    /// Возвращает специальный параметр Dispatcher-протокола, связанный с тегом.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns>Специальный параметр или <see langword="null"/>, если он не задан.</returns>
    private static SpecialParameter? GetSpecialParameter(EmulatorTagEntity tag)
    {
        return string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);
    }

    /// <summary>
    /// Преобразует конфигурацию интервального триггера в <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="trigger">Конфигурация запуска тега.</param>
    /// <returns>Интервал запуска тега.</returns>
    private static TimeSpan ToTimeSpan(TagTriggerDto trigger)
    {
        var value = Math.Max(1, trigger.IntervalValue ?? 1);
        return trigger.IntervalUnit switch
        {
            TagIntervalUnit.Ms => TimeSpan.FromMilliseconds(value),
            TagIntervalUnit.Min => TimeSpan.FromMinutes(value),
            _ => TimeSpan.FromSeconds(value),
        };
    }

    /// <summary>
    /// Возвращает идентификатор станка для Dispatcher-протокола.
    /// </summary>
    /// <param name="emulator">Эмулятор, публикующий мониторинг.</param>
    /// <returns>Идентификатор интеграции станка.</returns>
    private static object GetMachineIntegrationId(EmulatorEntity emulator) => emulator.ProtocolId;

    /// <summary>
    /// Находит CNC-программу, связанную со специальным тегом имени программы.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="values">Снимок рассчитанных значений тегов.</param>
    /// <param name="specialParameter">Специальный параметр, из которого берется имя программы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Программа для отправки в Dispatcher или <see langword="null"/>.</returns>
    private async Task<DispatcherProgram?> ResolveProgramAsync(
        string emulatorId,
        IReadOnlyList<GeneratedTagValue> values,
        SpecialParameter specialParameter,
        CancellationToken cancellationToken)
    {
        var programName = values
            .FirstOrDefault(value => value.SpecialParameter == specialParameter)
            ?.Value
            ?.ToString();

        if (string.IsNullOrWhiteSpace(programName))
        {
            return null;
        }

        var programs = await dataCache.GetVisibleCncProgramsAsync(emulatorId, cancellationToken);
        var match = ResolveProgram(programs, emulatorId, programName);

        return match is null ? null : TelemetryPacketSender.FromTextProgram(match.Name, match.Content);
    }

    /// <summary>
    /// Обрабатывает ответ Dispatcher после отправки мониторинга и выполняет обмен CNC-программами при запросе.
    /// </summary>
    /// <param name="emulator">Эмулятор, по которому выполнялась публикация.</param>
    /// <param name="machineIntegrationId">Идентификатор станка в Dispatcher-протоколе.</param>
    /// <param name="mainProgram">Основная программа для возможной отправки.</param>
    /// <param name="subProgram">Подпрограмма для возможной отправки.</param>
    /// <param name="answer">Разобранный ответ Dispatcher.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    private async Task HandleDispatcherAnswerAsync(
        EmulatorEntity emulator,
        object machineIntegrationId,
        DispatcherProgram? mainProgram,
        DispatcherProgram? subProgram,
        DispatcherMonitoringAnswer answer,
        CancellationToken cancellationToken)
    {
        if (answer.FileType == 1)
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, mainProgram, cancellationToken);
        }

        if (answer.FileType == 2)
        {
            await sender.SendProgramAsync(emulator.TargetUrl, machineIntegrationId, useInnerId: true, subProgram, cancellationToken);
        }

        if (answer.GetFile == 1)
        {
            var received = await sender.ReceiveProgramAsync(emulator.TargetUrl, machineIntegrationId, cancellationToken);
            if (received is not null)
            {
                UpsertReceivedProgram(emulator.Id, received);
                StoreReceivedProgramName(emulator, received.Name);
            }
        }
    }

    /// <summary>
    /// Сохраняет имя полученной от Dispatcher программы в статический тег имени программы.
    /// </summary>
    /// <param name="emulator">Эмулятор с тегами мониторинга.</param>
    /// <param name="programName">Имя полученной программы.</param>
    private void StoreReceivedProgramName(EmulatorEntity emulator, string programName)
    {
        var tag = emulator.Tags.FirstOrDefault(tag =>
            tag.Enabled &&
            GetSpecialParameter(tag) == SpecialParameter.PrgName &&
            UniEmuJson.EnumValue<TagSource>(tag.Source) == TagSource.Static);

        if (tag is null)
            return;

        tag.Preview = programName;
        stateStore.Set(emulator.Id, tag.Id, tag.Name, programName, numericValue: null, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Добавляет или обновляет CNC-программу, полученную от Dispatcher.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора-владельца программы.</param>
    /// <param name="program">Полученная программа Dispatcher.</param>
    private void UpsertReceivedProgram(string emulatorId, DispatcherProgram program)
    {
        var content = System.Text.Encoding.UTF8.GetString(program.Bytes);
        var existing = db.CncPrograms.FirstOrDefault(item =>
            item.EmulatorId == emulatorId &&
            item.Name == program.Name);

        if (existing is null)
        {
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = $"cnc-{Guid.NewGuid():N}"[..13],
                Name = program.Name,
                Scope = UniEmuJson.EnumString(CncScope.Emulator),
                EmulatorId = emulatorId,
                Description = "[dispatcher-received] Получена от Dispatcher",
                Content = content,
                SizeBytes = program.Bytes.Length,
                UpdatedAt = DateTimeOffset.UtcNow,
                UploadedAt = DateTimeOffset.UtcNow,
            });
            dataCache.InvalidateCncPrograms();
            return;
        }

        existing.Description = "[dispatcher-received] Получена от Dispatcher";
        existing.Content = content;
        existing.SizeBytes = program.Bytes.Length;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        dataCache.InvalidateCncPrograms();
    }

    /// <summary>
    /// Выбирает видимую CNC-программу по имени с приоритетом полученных и привязанных к эмулятору программ.
    /// </summary>
    /// <param name="programs">Список видимых программ.</param>
    /// <param name="emulatorId">Идентификатор текущего эмулятора.</param>
    /// <param name="programName">Искомое имя программы.</param>
    /// <returns>Подходящая программа или <see langword="null"/>.</returns>
    private static CncProgramEntity? ResolveProgram(
        IReadOnlyList<CncProgramEntity> programs,
        string emulatorId,
        string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName))
        {
            return null;
        }

        return programs
            .Where(program => program.Name.Equals(programName, StringComparison.OrdinalIgnoreCase))
            .Where(program => program.Scope == UniEmuJson.EnumString(CncScope.Shared) || program.EmulatorId == emulatorId)
            .OrderByDescending(program => program.Description.StartsWith("[dispatcher-received]", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(program => program.EmulatorId == emulatorId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Возвращает строковое значение тега, связанного со специальным параметром.
    /// </summary>
    /// <param name="values">Снимок рассчитанных значений тегов.</param>
    /// <param name="specialParameter">Искомый специальный параметр.</param>
    /// <returns>Строковое значение тега или <see langword="null"/>.</returns>
    private static string? GetSpecialStringValue(IReadOnlyList<GeneratedTagValue> values, SpecialParameter specialParameter)
    {
        return values
            .FirstOrDefault(value => value.SpecialParameter == specialParameter)
            ?.Value
            ?.ToString();
    }

    /// <summary>
    /// Проверяет, должен ли тег заполняться расчетом текущего кадра CNC-программы.
    /// </summary>
    /// <param name="tag">Конфигурация тега.</param>
    /// <returns><see langword="true"/>, если тег представляет номер или текст кадра.</returns>
    private static bool IsCalculatedProgramFrameTag(EmulatorTagEntity tag)
    {
        var specialParameter = GetSpecialParameter(tag);
        if (specialParameter is not (SpecialParameter.FrameNum or SpecialParameter.FrameText))
        {
            return false;
        }

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source is TagSource.Static or TagSource.Scenario;
    }

    /// <summary>
    /// Рассчитывает номер и текст текущего кадра программы по времени работы эмулятора.
    /// </summary>
    /// <param name="emulator">Эмулятор, задающий старт и интервал публикации.</param>
    /// <param name="timestamp">Время текущей публикации.</param>
    /// <param name="program">Выбранная CNC-программа.</param>
    /// <returns>Номер и текст текущего кадра.</returns>
    private static ProgramFrame CalculateProgramFrame(
        EmulatorEntity emulator,
        DateTimeOffset timestamp,
        CncProgramEntity? program)
    {
        var lines = SplitProgramLines(program?.Content);
        if (lines.Length == 0)
        {
            return new ProgramFrame(0, string.Empty);
        }

        var elapsedSec = emulator.StartedAt is null
            ? 0
            : Math.Max(0, (timestamp - emulator.StartedAt.Value).TotalSeconds);
        var stepSeconds = Math.Max(1, emulator.IntervalSec);
        var frameNumber = (int)(Math.Floor(elapsedSec / stepSeconds) % lines.Length);

        return new ProgramFrame(frameNumber, lines[frameNumber]);
    }

    /// <summary>
    /// Разбивает текст программы на строки без завершающей пустой строки.
    /// </summary>
    /// <param name="content">Содержимое CNC-программы.</param>
    /// <returns>Массив строк программы.</returns>
    private static string[] SplitProgramLines(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            return lines[..^1];
        }

        return lines;
    }

    /// <summary>
    /// Описывает текущий кадр CNC-программы.
    /// </summary>
    /// <param name="Number">Номер кадра.</param>
    /// <param name="Text">Текст кадра.</param>
    private sealed record ProgramFrame(int Number, string Text);
}
