using Microsoft.EntityFrameworkCore;
using Quartz;
using Microsoft.Extensions.Options;
using Quartz.Impl.Matchers;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Hosting;
using UniEmu.Mapping;
using UniEmu.Realtime;

namespace UniEmu.Runtime;

/// <summary>
/// Управляет расписанием Quartz-задач для запущенных эмуляторов и событийных тегов.
/// </summary>
public sealed class EmulatorScheduleService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    ISchedulerFactory schedulerFactory,
    TagRuntimeStateStore stateStore,
    TagPreviewFlushService previewFlushService,
    ILogger<EmulatorScheduleService> logger,
    IOptions<UniEmuOptions> options,
    TelemetryValueGenerator valueGenerator,
    TagScriptExecutionService scriptExecutionService,
    RuntimeUpdateService runtimeUpdateService)
{
    /// <summary>
    /// Восстанавливает расписание всех эмуляторов, которые были запущены до старта backend.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операций Quartz и базы данных.</param>
    /// <returns>Задача восстановления расписаний.</returns>
    public async Task ScheduleRunningEmulatorsAsync(CancellationToken cancellationToken = default)
    {
        var emulatorIds = await db.Emulators
            .AsNoTracking()
            .Where(e => e.Status == nameof(EmulatorStatus.Running))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        foreach (var emulatorId in emulatorIds)
        {
            await ScheduleEmulatorAsync(emulatorId, cancellationToken);
        }
    }

    /// <summary>
    /// Пересоздает расписание эмулятора, если он находится в состоянии Running.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операций Quartz и базы данных.</param>
    /// <returns>Задача проверки и пересоздания расписания.</returns>
    public async Task RescheduleIfRunningAsync(string emulatorId, CancellationToken cancellationToken = default)
    {
        var isRunning = await db.Emulators
            .AsNoTracking()
            .AnyAsync(e => e.Id == emulatorId && e.Status == nameof(EmulatorStatus.Running), cancellationToken);

        if (isRunning)
        {
            await ScheduleEmulatorAsync(emulatorId, cancellationToken);
        }
    }

    /// <summary>
    /// Пересоздает Quartz-задачи тегов, публикации и проверки блокировки для одного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операций Quartz и базы данных.</param>
    /// <returns>Задача создания расписания эмулятора.</returns>
    public async Task ScheduleEmulatorAsync(string emulatorId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await DeleteEmulatorJobsAsync(scheduler, emulatorId, cancellationToken);
        await NormalizeStoredTriggersAsync(emulatorId, cancellationToken);

        var emulator = await dataCache.GetEmulatorWithTagsAsync(emulatorId, cancellationToken);

        if (emulator is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.ClearEmulator(emulatorId);
            return;
        }

        foreach (var tag in emulator.Tags)
        {
            var trigger = UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
                ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);

            if (!ShouldScheduleTag(tag, trigger))
            {
                continue;
            }

            await ScheduleTagJobAsync(scheduler, emulator, tag, trigger, cancellationToken);
        }

        await SchedulePublishJobAsync(scheduler, emulator, cancellationToken);
        await ScheduleDispatcherBlockCheckJobAsync(scheduler, emulator, GetDispatcherBlockCheckInterval(options.Value), cancellationToken);
    }

    /// <summary>
    /// Удаляет Quartz-задачи эмулятора, очищает runtime-состояние и сбрасывает накопленные preview.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операций Quartz и базы данных.</param>
    /// <returns>Задача удаления расписания эмулятора.</returns>
    public async Task UnscheduleEmulatorAsync(string emulatorId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await DeleteEmulatorJobsAsync(scheduler, emulatorId, cancellationToken);
        stateStore.ClearEmulator(emulatorId);
        await previewFlushService.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Выполняет теги с событийным триггером без постановки отдельной Quartz-задачи.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="triggerEvent">Событие запуска тегов.</param>
    /// <param name="cancellationToken">Токен отмены расчета.</param>
    /// <returns>Задача выполнения событийных тегов.</returns>
    public async Task ExecuteEventTagsAsync(
        string emulatorId,
        TagTriggerEvent triggerEvent,
        CancellationToken cancellationToken = default)
    {
        var emulator = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (emulator is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var matchingTags = emulator.Tags
            .Where(tag =>
            {
                var trigger = UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
                    ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);

                return trigger.Mode == TagTriggerMode.Once && (trigger.Event ?? TagTriggerEvent.OnStart) == triggerEvent;
            })
            .ToList();

        foreach (var tag in matchingTags)
        {
            try
            {
                var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
                var value = await GenerateTagValueAsync(emulator, tag, source, now, cancellationToken);

                tag.Preview = TelemetryValueGenerator.ToPreview(value.Value);
                stateStore.Set(emulator.Id, tag.Id, tag.Name, value.Value, value.NumericValue, now);
                await runtimeUpdateService.PublishTagValueAsync(
                    new RuntimeTagValueUpdateDto(emulator.Id, tag.Id, tag.Name, value.Value, value.NumericValue, now),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Event tag generation failed for tag {TagId}", tag.Id);
                var message = $"Ошибка вычисления события тега {tag.Name}: {ex.Message}";
                emulator.LastError = message;
                db.SystemEvents.Add(new SystemEventEntity
                {
                    Id = $"ev-{Guid.NewGuid():N}"[..12],
                    EmulatorId = emulator.Id,
                    EmulatorName = emulator.Name,
                    Level = UniEmuJson.EnumString(EventLevel.Error),
                    Message = message,
                    Timestamp = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(emulator.LastError))
        {
            dataCache.InvalidateEmulator(emulator.Id);
            await runtimeUpdateService.PublishEmulatorUpdatedAsync(emulator.ToDto(emulator.Tags.Count), cancellationToken);
        }
    }

    private async Task<GeneratedTagValue> GenerateTagValueAsync(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        TagSource source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (source == TagSource.FormulaScript)
        {
            var generated = valueGenerator.GenerateTag(emulator, tag, timestamp);
            return await scriptExecutionService.GenerateScriptTagAsync(
                emulator,
                tag,
                timestamp,
                cancellationToken,
                generated.Value);
        }

        return source is TagSource.Script or TagSource.Formula
            ? await scriptExecutionService.GenerateScriptTagAsync(emulator, tag, timestamp, cancellationToken)
            : valueGenerator.GenerateTag(emulator, tag, timestamp);
    }

    private static async Task DeleteEmulatorJobsAsync(IScheduler scheduler, string emulatorId, CancellationToken cancellationToken)
    {
        await scheduler.DeleteJob(RuntimeJobKeys.PublishJob(emulatorId), cancellationToken);
        await scheduler.DeleteJob(RuntimeJobKeys.DispatcherBlockCheckJob(emulatorId), cancellationToken);

        var tagJobs = await scheduler.GetJobKeys(
            GroupMatcher<JobKey>.GroupEquals(RuntimeJobKeys.TagGroup(emulatorId)),
            cancellationToken);

        if (tagJobs.Count > 0)
        {
            await scheduler.DeleteJobs(tagJobs.ToList(), cancellationToken);
        }
    }

    private async Task NormalizeStoredTriggersAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var tags = await db.EmulatorTags
            .Where(tag => tag.EmulatorId == emulatorId)
            .ToListAsync(cancellationToken);
        var changed = false;

        foreach (var tag in tags)
        {
            var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
            var trigger = UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
                ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);
            var normalized = TagTriggerNormalizer.Normalize(source, trigger);
            if (normalized == trigger)
            {
                continue;
            }

            tag.TriggerJson = UniEmuJson.Serialize(normalized);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
    }

    private static async Task SchedulePublishJobAsync(IScheduler scheduler, EmulatorEntity emulator, CancellationToken cancellationToken)
    {
        var job = JobBuilder.Create<EmulatorPublishJob>()
            .WithIdentity(RuntimeJobKeys.PublishJob(emulator.Id))
            .UsingJobData(RuntimeJobKeys.EmulatorId, emulator.Id)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(RuntimeJobKeys.PublishTrigger(emulator.Id))
            .ForJob(job)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(TimeSpan.FromSeconds(Math.Max(1, emulator.IntervalSec)))
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    private static async Task ScheduleDispatcherBlockCheckJobAsync(
        IScheduler scheduler,
        EmulatorEntity emulator,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        var job = JobBuilder.Create<DispatcherBlockCheckJob>()
            .WithIdentity(RuntimeJobKeys.DispatcherBlockCheckJob(emulator.Id))
            .UsingJobData(RuntimeJobKeys.EmulatorId, emulator.Id)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(RuntimeJobKeys.DispatcherBlockCheckTrigger(emulator.Id))
            .ForJob(job)
            .StartNow()
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(interval)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    private async Task ScheduleTagJobAsync(
        IScheduler scheduler,
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        TagTriggerDto trigger,
        CancellationToken cancellationToken)
    {
        var job = JobBuilder.Create<TagValueJob>()
            .WithIdentity(RuntimeJobKeys.TagJob(emulator.Id, tag.Id))
            .UsingJobData(RuntimeJobKeys.EmulatorId, emulator.Id)
            .UsingJobData(RuntimeJobKeys.TagId, tag.Id)
            .Build();

        var quartzTrigger = BuildTagTrigger(emulator.Id, tag.Id, trigger);
        if (quartzTrigger is null)
        {
            logger.LogWarning("Skipping invalid trigger for tag {TagId}", tag.Id);
            var message = $"Некорректное расписание тега {tag.Name}";
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = message,
                Timestamp = DateTimeOffset.UtcNow,
            });

            var trackedEmulator = await db.Emulators
                .Include(e => e.Tags)
                .FirstOrDefaultAsync(e => e.Id == emulator.Id, cancellationToken);
            if (trackedEmulator is not null)
            {
                trackedEmulator.LastError = message;
                dataCache.InvalidateEmulator(trackedEmulator.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            if (trackedEmulator is not null)
            {
                await runtimeUpdateService.PublishEmulatorUpdatedAsync(
                    trackedEmulator.ToDto(trackedEmulator.Tags.Count),
                    cancellationToken);
            }

            return;
        }

        await scheduler.ScheduleJob(job, quartzTrigger, cancellationToken);
    }

    private static ITrigger? BuildTagTrigger(string emulatorId, string tagId, TagTriggerDto trigger)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(RuntimeJobKeys.TagTrigger(emulatorId, tagId))
            .ForJob(RuntimeJobKeys.TagJob(emulatorId, tagId))
            .StartNow();

        return trigger.Mode switch
        {
            TagTriggerMode.Interval => builder
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(ToTimeSpan(trigger))
                    .RepeatForever())
                .Build(),
            TagTriggerMode.Cron => BuildCronTrigger(builder, trigger.Cron),
            _ => null,
        };
    }

    private static ITrigger? BuildCronTrigger(TriggerBuilder builder, string? cron)
    {
        var normalized = NormalizeCron(cron);
        if (normalized is null || !CronExpression.IsValidExpression(normalized))
        {
            return null;
        }

        return builder
            .WithCronSchedule(normalized)
            .Build();
    }

    private static bool ShouldScheduleTag(EmulatorTagEntity tag, TagTriggerDto trigger)
    {
        return !IsCalculatedProgramFrameTag(tag)
               && (trigger.Mode is TagTriggerMode.Interval or TagTriggerMode.Cron);
    }

    private static bool IsCalculatedProgramFrameTag(EmulatorTagEntity tag)
    {
        var specialParameter = tag.SpecialParameter;
        if (!string.Equals(specialParameter, nameof(SpecialParameter.FrameNum), StringComparison.Ordinal)
            && !string.Equals(specialParameter, nameof(SpecialParameter.FrameText), StringComparison.Ordinal))
        {
            return false;
        }

        var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
        return source is TagSource.Static or TagSource.Scenario;
    }

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

    private static TimeSpan GetDispatcherBlockCheckInterval(UniEmuOptions options)
    {
        return TimeSpan.FromSeconds(Math.Clamp(options.DispatcherBlockCheckIntervalSeconds, 5, 10));
    }
}
