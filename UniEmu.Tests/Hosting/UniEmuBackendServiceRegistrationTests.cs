using Autofac;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Features.Common;
using UniEmu.Hosting;

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
}
