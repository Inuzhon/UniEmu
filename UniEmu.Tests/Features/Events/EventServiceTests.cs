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
using UniEmu.Tests.Common;

namespace UniEmu.Tests.Features.Events;

public sealed class EventServiceTests
{
    [Fact]
    public async Task ListAsync_AppliesCursorAndLimitInDatabaseQuery()
    {
        await using var fixture = await EventDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new EventService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var cursor = DateTimeOffset.Parse("2026-05-10T12:04:00Z");

        var events = await service.ListAsync(cursor, 2, CancellationToken.None);

        Assert.Equal(
            new[] { "ev-3", "ev-2" },
            events.Select(ev => ev.Id));

        var eventQuery = Assert.Single(
            fixture.Interceptor.CommandTexts,
            command => command.Contains("SystemEvents", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("WHERE", eventQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", eventQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenEmulatorDoesNotExist()
    {
        await using var fixture = await EventDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new EventService(db, new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()));
        var request = new PushEventRequest(
            "missing",
            "Missing emulator",
            EventLevel.Warn,
            "Unexpected event",
            DateTimeOffset.Parse("2026-05-10T12:10:00Z"));

        var created = await service.CreateAsync(request, CancellationToken.None);

        Assert.Null(created);
        Assert.False(await db.SystemEvents.AnyAsync(ev => ev.EmulatorId == "missing"));
    }

    private sealed class EventDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private EventDbFixture(
            SqliteConnection connection,
            DbContextOptions<UniEmuDbContext> options,
            RecordingDbCommandInterceptor interceptor)
        {
            this.connection = connection;
            this.options = options;
            Interceptor = interceptor;
        }

        public RecordingDbCommandInterceptor Interceptor { get; }

        public static async Task<EventDbFixture> CreateAsync()
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

            return new EventDbFixture(connection, options, interceptor);
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
                db.SystemEvents.Add(new SystemEventEntity
                {
                    Id = $"ev-{i}",
                    EmulatorId = "em-1",
                    EmulatorName = "Main emulator",
                    Level = UniEmuJson.EnumString(EventLevel.Info),
                    Message = $"Event {i}",
                    Timestamp = start.AddMinutes(i),
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
