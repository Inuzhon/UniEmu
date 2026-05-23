using Microsoft.AspNetCore.Mvc;
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

namespace UniEmu.Tests.Features.Telemetry;

public sealed class TelemetryControllerTests
{
    [Fact]
    public async Task Get_ReturnsOkWithTelemetry_WhenEmulatorExists()
    {
        await using var fixture = await TelemetryControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TelemetryController(CreateService(db));

        var result = await controller.Get("em-1", 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var telemetry = Assert.IsAssignableFrom<IReadOnlyList<TelemetryPointDto>>(ok.Value);
        var point = Assert.Single(telemetry);
        Assert.Equal(DateTimeOffset.Parse("2026-05-10T12:00:00Z"), point.Timestamp);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await TelemetryControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TelemetryController(CreateService(db));

        var result = await controller.Get("missing", 10, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Ingest_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var controller = new TelemetryController(null!);

        var result = await controller.Ingest(
            new TelemetryIngestRequest(" ", DateTimeOffset.UtcNow, new Dictionary<string, object?>()),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("emulatorId and values are required.", badRequest.Value);
    }

    [Fact]
    public async Task Ingest_ReturnsOkWithCreatedTelemetry_WhenEmulatorExists()
    {
        await using var fixture = await TelemetryControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TelemetryController(CreateService(db));
        var timestamp = DateTimeOffset.Parse("2026-05-10T13:30:00Z");

        var result = await controller.Ingest(
            new TelemetryIngestRequest("em-1", timestamp, new Dictionary<string, object?> { ["Load"] = 75 }),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var telemetry = Assert.IsType<TelemetryPointDto>(ok.Value);
        Assert.Equal(timestamp, telemetry.Timestamp);
        Assert.True(await db.TelemetryPoints.AnyAsync(point => point.Timestamp == timestamp));
    }

    [Fact]
    public async Task Ingest_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await TelemetryControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var controller = new TelemetryController(CreateService(db));

        var result = await controller.Ingest(
            new TelemetryIngestRequest("missing", DateTimeOffset.UtcNow, new Dictionary<string, object?> { ["Load"] = 75 }),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TelemetryService CreateService(UniEmuDbContext db)
    {
        return new TelemetryService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
    }

    private sealed class TelemetryControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private TelemetryControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<TelemetryControllerDbFixture> CreateAsync()
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
            db.TelemetryPoints.Add(new TelemetryPointEntity
            {
                EmulatorId = "em-1",
                Timestamp = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
                ValuesJson = UniEmuJson.Serialize(new Dictionary<string, object?> { ["Load"] = 50 }),
            });
            await db.SaveChangesAsync();

            return new TelemetryControllerDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
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
