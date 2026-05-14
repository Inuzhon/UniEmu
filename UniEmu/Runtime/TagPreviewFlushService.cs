using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using UniEmu.Data;

namespace UniEmu.Runtime;

public sealed class TagPreviewFlushService(
    Func<UniEmuDbContext> dbContextFactory,
    ILogger<TagPreviewFlushService> logger)
{
    private readonly ConcurrentDictionary<TagPreviewKey, string> dirtyPreviews = new();

    public void MarkDirty(string emulatorId, string tagId, string preview)
    {
        dirtyPreviews[new TagPreviewKey(emulatorId, tagId)] = preview;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var batch = dirtyPreviews.ToArray();
        if (batch.Length == 0)
        {
            return;
        }

        foreach (var item in batch)
        {
            dirtyPreviews.TryRemove(item.Key, out _);
        }

        try
        {
            await using var db = dbContextFactory();
            foreach (var item in batch)
            {
                await db.EmulatorTags
                    .Where(t => t.EmulatorId == item.Key.EmulatorId && t.Id == item.Key.TagId)
                    .ExecuteUpdateAsync(
                        update => update.SetProperty(t => t.Preview, item.Value),
                        cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            foreach (var item in batch)
            {
                dirtyPreviews[item.Key] = item.Value;
            }

            logger.LogWarning(ex, "Failed to flush tag previews");
        }
    }

    private sealed record TagPreviewKey(string EmulatorId, string TagId);
}
