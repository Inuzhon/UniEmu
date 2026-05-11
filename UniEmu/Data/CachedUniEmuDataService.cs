using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

public sealed class CachedUniEmuDataService(
    UniEmuDbContext db,
    IMemoryCache cache)
{
    private static readonly MemoryCacheEntryOptions s_cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
        SlidingExpiration = TimeSpan.FromSeconds(20),
    };

    public Task<EmulatorEntity?> GetEmulatorWithTagsAsync(string emulatorId, CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            EmulatorKey(emulatorId),
            entry =>
            {
                entry.SetOptions(s_cacheOptions);
                return db.Emulators
                    .AsNoTracking()
                    .Include(e => e.Tags.OrderBy(t => t.Name))
                    .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);
            });
    }

    public async Task<IReadOnlyList<ScriptFileEntity>> GetVisibleScriptsAsync(
        string emulatorId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            VisibleScriptsKey(emulatorId),
            async entry =>
            {
                entry.SetOptions(s_cacheOptions);
                var scripts = await db.ScriptFiles
                    .AsNoTracking()
                    .Where(s => s.Scope == "shared" || s.EmulatorId == emulatorId)
                    .OrderBy(s => s.Scope == "shared" ? 0 : 1)
                    .ThenBy(s => s.Name)
                    .ToListAsync(cancellationToken);

                return scripts;
            }) ?? [];
    }

    public async Task<IReadOnlyList<CncProgramEntity>> GetVisibleCncProgramsAsync(
        string emulatorId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            VisibleCncProgramsKey(emulatorId),
            async entry =>
            {
                entry.SetOptions(s_cacheOptions);
                var programs = await db.CncPrograms
                    .AsNoTracking()
                    .Where(p => p.Scope == "shared" || p.EmulatorId == emulatorId)
                    .OrderBy(p => p.Scope == "shared" ? 0 : 1)
                    .ThenBy(p => p.Name)
                    .ToListAsync(cancellationToken);

                return programs;
            }) ?? [];
    }

    public void InvalidateEmulator(string emulatorId)
    {
        cache.Remove(EmulatorKey(emulatorId));
        cache.Remove(VisibleScriptsKey(emulatorId));
        cache.Remove(VisibleCncProgramsKey(emulatorId));
    }

    public void InvalidateScripts()
    {
        cache.Remove(ScriptCacheVersionKey);
        cache.Set(ScriptCacheVersionKey, Guid.NewGuid().ToString("N"), s_cacheOptions);
    }

    public void InvalidateCncPrograms()
    {
        cache.Remove(CncProgramCacheVersionKey);
        cache.Set(CncProgramCacheVersionKey, Guid.NewGuid().ToString("N"), s_cacheOptions);
    }

    private string GetScriptCacheVersion()
    {
        return cache.GetOrCreate(
            ScriptCacheVersionKey,
            entry =>
            {
                entry.SetOptions(s_cacheOptions);
                return Guid.NewGuid().ToString("N");
            })!;
    }

    private string GetCncProgramCacheVersion()
    {
        return cache.GetOrCreate(
            CncProgramCacheVersionKey,
            entry =>
            {
                entry.SetOptions(s_cacheOptions);
                return Guid.NewGuid().ToString("N");
            })!;
    }

    private static string EmulatorKey(string emulatorId) => $"uniemu:data:emulator:{emulatorId}";

    private string VisibleScriptsKey(string emulatorId) => $"uniemu:data:scripts:{GetScriptCacheVersion()}:{emulatorId}";

    private string VisibleCncProgramsKey(string emulatorId) => $"uniemu:data:cnc-programs:{GetCncProgramCacheVersion()}:{emulatorId}";

    private const string ScriptCacheVersionKey = "uniemu:data:scripts:version";

    private const string CncProgramCacheVersionKey = "uniemu:data:cnc-programs:version";
}
