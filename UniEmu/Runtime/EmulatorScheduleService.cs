using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;

namespace UniEmu.Runtime;

public sealed class EmulatorScheduleService(
    UniEmuDbContext db,
    ISchedulerFactory schedulerFactory,
    TagRuntimeStateStore stateStore,
    ILogger<EmulatorScheduleService> logger)
{
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

    public async Task ScheduleEmulatorAsync(string emulatorId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await DeleteEmulatorJobsAsync(scheduler, emulatorId, cancellationToken);

        var emulator = await db.Emulators
            .AsNoTracking()
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (emulator is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.ClearEmulator(emulatorId);
            return;
        }

        foreach (var tag in emulator.Tags)
        {
            var trigger = UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
                ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);

            if (!ShouldScheduleTag(emulator, trigger))
            {
                continue;
            }

            await ScheduleTagJobAsync(scheduler, emulator, tag, trigger, cancellationToken);
        }

        await SchedulePublishJobAsync(scheduler, emulator, cancellationToken);
    }

    public async Task UnscheduleEmulatorAsync(string emulatorId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await DeleteEmulatorJobsAsync(scheduler, emulatorId, cancellationToken);
        stateStore.ClearEmulator(emulatorId);
    }

    private static async Task DeleteEmulatorJobsAsync(IScheduler scheduler, string emulatorId, CancellationToken cancellationToken)
    {
        await scheduler.DeleteJob(RuntimeJobKeys.PublishJob(emulatorId), cancellationToken);

        var tagJobs = await scheduler.GetJobKeys(
            GroupMatcher<JobKey>.GroupEquals(RuntimeJobKeys.TagGroup(emulatorId)),
            cancellationToken);

        if (tagJobs.Count > 0)
        {
            await scheduler.DeleteJobs(tagJobs.ToList(), cancellationToken);
        }
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
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = $"Некорректное расписание тега {tag.Name}",
                Timestamp = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
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
            TagTriggerMode.Once => builder.Build(),
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

    private static bool ShouldScheduleTag(EmulatorEntity emulator, TagTriggerDto trigger)
    {
        return trigger.Mode is TagTriggerMode.Once or TagTriggerMode.Interval or TagTriggerMode.Cron;
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
}
