using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniEmu.Data;

namespace UniEmu.Runtime;

/// <summary>
/// Буферизует изменения preview тегов и пакетно записывает их в базу данных.
/// </summary>
public sealed class TagPreviewFlushService
{
    private readonly ConcurrentDictionary<TagPreviewKey, string> dirtyPreviews = new();
    private readonly Func<DbContextLease> dbContextFactory;
    private readonly ILogger<TagPreviewFlushService> logger;

    /// <summary>
    /// Создает сервис с контекстом базы данных из DI-scope.
    /// </summary>
    /// <param name="scopeFactory">Фабрика DI-scope для фоновой записи.</param>
    /// <param name="logger">Логгер ошибок отложенной записи.</param>
    public TagPreviewFlushService(
        IServiceScopeFactory scopeFactory,
        ILogger<TagPreviewFlushService> logger)
        : this(
            () =>
            {
                var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<UniEmuDbContext>();
                return new DbContextLease(db, scope, disposeDbContext: true);
            },
            logger)
    {
    }

    /// <summary>
    /// Создает сервис с явной фабрикой контекста базы данных.
    /// </summary>
    /// <param name="dbContextFactory">Фабрика контекста базы данных.</param>
    /// <param name="logger">Логгер ошибок отложенной записи.</param>
    public TagPreviewFlushService(
        Func<UniEmuDbContext> dbContextFactory,
        ILogger<TagPreviewFlushService> logger)
        : this(() => new DbContextLease(dbContextFactory(), null, disposeDbContext: false), logger)
    {
    }

    private TagPreviewFlushService(
        Func<DbContextLease> dbContextFactory,
        ILogger<TagPreviewFlushService> logger)
    {
        this.dbContextFactory = dbContextFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Помечает preview тега как измененный без немедленной записи в базу.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <param name="preview">Новое preview-значение.</param>
    public void MarkDirty(string emulatorId, string tagId, string preview)
    {
        dirtyPreviews[new TagPreviewKey(emulatorId, tagId)] = preview;
    }

    /// <summary>
    /// Выполняет отложенную запись всех накопленных preview и возвращает их в очередь при ошибке.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции записи.</param>
    /// <returns>Задача записи накопленных preview.</returns>
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
            await using var lease = dbContextFactory();
            var db = lease.DbContext;
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

    private sealed class DbContextLease(UniEmuDbContext dbContext, IServiceScope? scope, bool disposeDbContext) : IAsyncDisposable
    {
        public UniEmuDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            if (disposeDbContext)
            {
                await DbContext.DisposeAsync();
            }

            scope?.Dispose();
        }
    }
}
