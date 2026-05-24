using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
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

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));

        var telemetry = await service.GetAsync("missing", 10, CancellationToken.None);

        Assert.Null(telemetry);
    }

    [Fact]
    public async Task GetAsync_UsesDefaultLimit_WhenRequestedPointsIsNotPositive()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));

        var telemetry = await service.GetAsync("em-1", 0, CancellationToken.None);

        Assert.NotNull(telemetry);
        Assert.Equal(5, telemetry.Count);
    }

    [Fact]
    public async Task IngestAsync_CreatesTelemetryAndPublishesRuntimeUpdate()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var broadcaster = new RecordingRuntimeUpdateBroadcaster();
        var service = new TelemetryService(db, new RuntimeUpdateService(broadcaster));
        var timestamp = DateTimeOffset.Parse("2026-05-10T13:00:00Z");

        var telemetry = await service.IngestAsync(
            new TelemetryIngestRequest(
                "em-1",
                timestamp,
                new Dictionary<string, object?> { ["Power"] = 12.5, ["Mode"] = "Auto" }),
            CancellationToken.None);

        Assert.NotNull(telemetry);
        Assert.Equal(timestamp, telemetry.Timestamp);
        Assert.Equal(["Mode", "Power"], telemetry.Values.Keys.OrderBy(key => key));

        var stored = await db.TelemetryPoints
            .Where(point => point.Timestamp == timestamp)
            .SingleAsync();
        Assert.Contains("\"Power\":12.5", stored.ValuesJson);
        var update = Assert.Single(broadcaster.TelemetryUpdates);
        Assert.Equal("em-1", update.Update.EmulatorId);
        Assert.Equal(timestamp, update.Update.Point.Timestamp);
        Assert.Contains("emulator:em-1", update.Groups);
    }

    [Fact]
    public async Task IngestAsync_UsesCurrentTime_WhenTimestampIsDefault()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var before = DateTimeOffset.UtcNow;

        var telemetry = await service.IngestAsync(
            new TelemetryIngestRequest("em-1", default, new Dictionary<string, object?> { ["Value"] = 1 }),
            CancellationToken.None);

        Assert.NotNull(telemetry);
        Assert.True(telemetry.Timestamp >= before);
        Assert.True(telemetry.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task IngestAsync_ReturnsNull_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await TelemetryDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));

        var telemetry = await service.IngestAsync(
            new TelemetryIngestRequest("missing", DateTimeOffset.UtcNow, new Dictionary<string, object?> { ["Value"] = 1 }),
            CancellationToken.None);

        Assert.Null(telemetry);
        Assert.Equal(5, await db.TelemetryPoints.CountAsync());
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

    private sealed class RecordingRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public List<(RuntimeTelemetryUpdateDto Update, IReadOnlyList<string> Groups)> TelemetryUpdates { get; } = [];

        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            TelemetryUpdates.Add((update, groups));
            return Task.CompletedTask;
        }

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
