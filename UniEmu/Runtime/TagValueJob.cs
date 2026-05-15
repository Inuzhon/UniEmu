using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Realtime;

namespace UniEmu.Runtime;

[DisallowConcurrentExecution]
public sealed class TagValueJob(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    TelemetryValueGenerator valueGenerator,
    TagScriptExecutionService scriptExecutionService,
    TagRuntimeStateStore stateStore,
    TagPreviewFlushService previewFlushService,
    RuntimeUpdateService runtimeUpdateService,
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

        var emulator = await dataCache.GetEmulatorWithTagsAsync(emulatorId, cancellationToken);
        var tag = emulator?.Tags.FirstOrDefault(t => t.Id == tagId);

        if (emulator is null || tag is null || emulator.Status != nameof(EmulatorStatus.Running))
        {
            stateStore.Remove(emulatorId, tagId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
            var value = await GenerateTagValueAsync(emulator, tag, source, now, cancellationToken);

            var preview = TelemetryValueGenerator.ToPreview(value.Value);
            tag.Preview = preview;
            stateStore.Set(emulatorId, tagId, tag.Name, value.Value, value.NumericValue, now);
            previewFlushService.MarkDirty(emulatorId, tagId, preview);
            await runtimeUpdateService.PublishTagValueAsync(
                new RuntimeTagValueUpdateDto(emulatorId, tagId, tag.Name, value.Value, value.NumericValue, now),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tag value generation failed for tag {TagId}", tagId);
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = $"ev-{Guid.NewGuid():N}"[..12],
                EmulatorId = emulator.Id,
                EmulatorName = emulator.Name,
                Level = UniEmuJson.EnumString(EventLevel.Error),
                Message = $"Ошибка вычисления тега {tag.Name}: {ex.Message}",
                Timestamp = now,
            });

            await db.SaveChangesAsync(cancellationToken);
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
}
