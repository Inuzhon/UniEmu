using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;

namespace UniEmu.Runtime;

public sealed class TagRuntimeStatePersistenceService(
    UniEmuDbContext db,
    TagRuntimeStateStore stateStore)
{
    public async Task HydrateFromTagPreviewsAsync(CancellationToken cancellationToken = default)
    {
        var tags = await db.EmulatorTags
            .AsNoTracking()
            .Where(tag => tag.Preview != string.Empty)
            .ToListAsync(cancellationToken);
        var timestamp = DateTimeOffset.UtcNow;

        foreach (var tag in tags)
        {
            var trigger = UniEmuJson.Deserialize<TagTriggerDto>(tag.TriggerJson)
                ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);
            var source = UniEmuJson.EnumValue<TagSource>(tag.Source);
            if (source != TagSource.Static && trigger.Mode is not (TagTriggerMode.Once or TagTriggerMode.Cron))
            {
                continue;
            }

            var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
            var value = TelemetryValueGenerator.FromPreview(tagType, tag.Preview);
            stateStore.Set(
                tag.EmulatorId,
                tag.Id,
                tag.Name,
                value,
                TelemetryValueGenerator.ToNumericValue(value),
                timestamp);
        }
    }

    public async Task PersistToTagPreviewsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = stateStore.Snapshot();
        if (snapshot.Count == 0)
        {
            return;
        }

        var ids = snapshot.Select(value => value.TagId).ToHashSet(StringComparer.Ordinal);
        var tags = await db.EmulatorTags
            .Where(tag => ids.Contains(tag.Id))
            .ToDictionaryAsync(tag => tag.Id, StringComparer.Ordinal, cancellationToken);

        foreach (var value in snapshot)
        {
            if (tags.TryGetValue(value.TagId, out var tag) && tag.EmulatorId == value.EmulatorId)
            {
                tag.Preview = TelemetryValueGenerator.ToPreview(value.Value);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
