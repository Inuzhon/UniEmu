using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Features.Events;
using UniEmu.Realtime;

namespace UniEmu.Tests.Features.Events;

public sealed class EventsControllerTests
{
    [Fact]
    public async Task List_ReturnsOkWithEvents()
    {
        await using var fixture = await EventsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new EventService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var controller = new EventsController(service);

        var result = await controller.List(null, 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var events = Assert.IsAssignableFrom<IReadOnlyList<SystemEventDto>>(ok.Value);
        var ev = Assert.Single(events);
        Assert.Equal("Startup", ev.Message);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var controller = new EventsController(null!);

        var result = await controller.Create(
            new PushEventRequest(" ", "Main emulator", EventLevel.Info, " ", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("emulatorId and message are required.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenEmulatorExists()
    {
        await using var fixture = await EventsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new EventService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var controller = new EventsController(service);
        var timestamp = DateTimeOffset.Parse("2026-05-10T13:00:00Z");

        var result = await controller.Create(
            new PushEventRequest("em-1", "Main emulator", EventLevel.Error, "Manual alarm", timestamp),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(EventsController.List), created.ActionName);
        var ev = Assert.IsType<SystemEventDto>(created.Value);
        Assert.Equal(EventLevel.Error, ev.Level);
        Assert.Equal("Manual alarm", ev.Message);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await EventsControllerDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new EventService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var controller = new EventsController(service);
        var request = new PushEventRequest(
            "missing",
            "Missing emulator",
            EventLevel.Warn,
            "Unexpected event",
            DateTimeOffset.Parse("2026-05-10T12:10:00Z"));

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class EventsControllerDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private EventsControllerDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<EventsControllerDbFixture> CreateAsync()
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
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = "ev-startup",
                EmulatorId = "em-1",
                EmulatorName = "Main emulator",
                Level = UniEmuJson.EnumString(EventLevel.Info),
                Message = "Startup",
                Timestamp = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            });
            await db.SaveChangesAsync();

            return new EventsControllerDbFixture(connection, options);
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
