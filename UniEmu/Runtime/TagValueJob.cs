using Microsoft.EntityFrameworkCore;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;

namespace UniEmu.Runtime;

[DisallowConcurrentExecution]
public sealed class TagValueJob(
    UniEmuDbContext db,
    TelemetryValueGenerator valueGenerator,
    TagScriptExecutionService scriptExecutionService,
    TagRuntimeStateStore stateStore,
    ILogger<TagValueJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var emulatorId = context.MergedJobDataMap.GetString(RuntimeJobKeys.EmulatorId);
        var tagId = context.MergedJobDataMap.GetString(RuntimeJobKeys.TagId);

        if (string.IsNullOrWhiteSpace(emulatorId) || string.IsNullOrWhiteSpace(tagId))
        {
            logger.LogWarning("Tag value job is missing emulatorId or tagId");
            return;
        }

        var tag = await db.EmulatorTags
            .Include(t => t.Emulator)
            .ThenInclude(e => e!.Tags)
            .FirstOrDefaultAsync(t => t.EmulatorId == emulatorId && t.Id == tagId, cancellationToken);

        if (tag?.Emulator is null || tag.Emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.Remove(emulatorId, tagId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
            var value = source is TagSource.Script or TagSource.Formula
                ? await scriptExecutionService.GenerateScriptTagAsync(tag.Emulator, tag, now, cancellationToken)
                : valueGenerator.GenerateTag(tag.Emulator, tag, now);

            stateStore.Set(emulatorId, tagId, tag.Name, value.Value, value.NumericValue, now);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tag value generation failed for tag {TagId}", tagId);
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = tag.Emulator.Id,
                EmulatorName = tag.Emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = $"Ошибка вычисления тега {tag.Name}: {ex.Message}",
                Timestamp = now,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
