using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Features.Events;
using UniEmu.Realtime;

namespace UniEmu.Tests.Features.Events;

public sealed class EventsControllerTests
{
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
