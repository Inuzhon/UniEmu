using Autofac;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniEmu.Data;
using UniEmu.Features.Common;
using UniEmu.Hosting;
using UniEmu.Runtime;
using UniEmu.Scripting.Api;

namespace UniEmu.Tests.Hosting;

public sealed class UniEmuBackendServiceRegistrationTests
{
    [Fact]
    public async Task RegisterServices_RegistersScopedResourceValidator()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        var builder = new ContainerBuilder();
        builder.RegisterInstance(new UniEmuDbContext(options)).AsSelf();

        UniEmuBackendServiceRegistration.RegisterServices(builder);

        await using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();

        Assert.NotNull(scope.Resolve<ScopedResourceValidator>());
    }

    [Fact]
    public async Task RegisterServices_ConfiguresTagScriptExecutionTimeoutFromOptions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        var builder = new ContainerBuilder();
        builder.RegisterInstance(new UniEmuDbContext(options)).AsSelf();
        builder.RegisterInstance(new MemoryCache(new MemoryCacheOptions())).As<IMemoryCache>();
        builder.RegisterInstance(Options.Create(new UniEmuOptions { ScriptExecutionTimeoutSeconds = 12 }))
            .As<IOptions<UniEmuOptions>>();

        UniEmuBackendServiceRegistration.RegisterServices(builder);
        builder.RegisterInstance(new NoopRestOperations()).As<ITagScriptRestOperations>();
        builder.RegisterInstance(new TagPreviewFlushService(
                () => new UniEmuDbContext(options),
                NullLogger<TagPreviewFlushService>.Instance))
            .AsSelf();

        await using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();

        var service = scope.Resolve<TagScriptExecutionService>();
        var field = typeof(TagScriptExecutionService).GetField(
            "scriptExecutionTimeout",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(TimeSpan.FromSeconds(12), field.GetValue(service));
    }

    private sealed class NoopRestOperations : ITagScriptRestOperations
    {
        public Task<Worker?> GetWorkerByIdAsync(int workerId, CancellationToken cancellationToken) => Task.FromResult<Worker?>(null);

        public Task<Worker?> GetActiveWorkerAsync(CancellationToken cancellationToken) => Task.FromResult<Worker?>(null);

        public Task RegisterWorkerAsync(int workerId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RestCallResult> TryRegisterWorkerAsync(int workerId, CancellationToken cancellationToken) =>
            Task.FromResult(RestCallResult.Failed(null, "REST is disabled in this test."));
    }
}
