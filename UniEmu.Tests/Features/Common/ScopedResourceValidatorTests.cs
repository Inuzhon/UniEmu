using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Common;

namespace UniEmu.Tests.Features.Common;

public sealed class ScopedResourceValidatorTests
{
    [Theory]
    [InlineData(ScriptScope.Shared, null, true)]
    [InlineData(ScriptScope.Shared, "em-1", false)]
    [InlineData(ScriptScope.Emulator, "em-1", true)]
    [InlineData(ScriptScope.Emulator, "missing", false)]
    [InlineData((ScriptScope)999, null, false)]
    public async Task IsValidScriptScopeAsync_ValidatesSharedAndEmulatorRules(
        ScriptScope scope,
        string? emulatorId,
        bool expected)
    {
        await using var fixture = await ScopedResourceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var validator = new ScopedResourceValidator(db);

        var valid = await validator.IsValidScriptScopeAsync(scope, emulatorId, CancellationToken.None);

        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData(CncScope.Shared, null, true)]
    [InlineData(CncScope.Shared, "em-1", false)]
    [InlineData(CncScope.Emulator, "em-1", true)]
    [InlineData(CncScope.Emulator, "missing", false)]
    [InlineData((CncScope)999, null, false)]
    public async Task IsValidCncScopeAsync_ValidatesSharedAndEmulatorRules(
        CncScope scope,
        string? emulatorId,
        bool expected)
    {
        await using var fixture = await ScopedResourceDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var validator = new ScopedResourceValidator(db);

        var valid = await validator.IsValidCncScopeAsync(scope, emulatorId, CancellationToken.None);

        Assert.Equal(expected, valid);
    }

    private sealed class ScopedResourceDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private ScopedResourceDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<ScopedResourceDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            await db.SaveChangesAsync();

            return new ScopedResourceDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
