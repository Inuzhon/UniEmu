using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Telemetry;
using UniEmu.Realtime;
using UniEmu.Tests.Common;

namespace UniEmu.Tests.Features.Telemetry;

public sealed class TelemetryServiceTests
{
    [Fact]
    public async Task GetAsync_AppliesLimitInDatabaseQuery()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));

        var telemetry = await service.GetAsync("em-1", 3, CancellationToken.None);

        Assert.NotNull(telemetry);
        var points = telemetry!;
        Assert.Equal(3, points.Count);
        Assert.Equal(
            new[]
            {
                DateTimeOffset.Parse("2026-05-10T12:02:00Z"),
                DateTimeOffset.Parse("2026-05-10T12:03:00Z"),
                DateTimeOffset.Parse("2026-05-10T12:04:00Z"),
            },
            points.Select(point => point.Timestamp));

        var telemetryQuery = Assert.Single(
            fixture.Interceptor.CommandTexts,
            command => command.Contains("TelemetryPoints", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("LIMIT", telemetryQuery, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TelemetryDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private TelemetryDbFixture(
            SqliteConnection connection,
            DbContextOptions<UniEmuDbContext> options,
            RecordingDbCommandInterceptor interceptor)
        {
            this.connection = connection;
            this.options = options;
            Interceptor = interceptor;
        }

        public RecordingDbCommandInterceptor Interceptor { get; }

        public static async Task<TelemetryDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var interceptor = new RecordingDbCommandInterceptor();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);
            interceptor.Clear();

            return new TelemetryDbFixture(connection, options, interceptor);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(UniEmuDbContext db)
        {
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            var start = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
            for (var i = 0; i < 5; i++)
            {
                db.TelemetryPoints.Add(new TelemetryPointEntity
                {
                    EmulatorId = "em-1",
                    Timestamp = start.AddMinutes(i),
                    ValuesJson = UniEmuJson.Serialize(new Dictionary<string, object?> { ["Value"] = i }),
                });
            }

            await db.SaveChangesAsync();
        }
    }

    private sealed class NoopRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
