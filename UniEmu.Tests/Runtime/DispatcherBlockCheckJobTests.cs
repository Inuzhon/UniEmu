using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class DispatcherBlockCheckJobTests
{
    [Fact]
    public async Task Execute_MarksRunningEmulatorAsError_WhenDispatcherBlocksProtocol()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Emulators.Add(new EmulatorEntity
        {
            Id = "em-1",
            Name = "Blocked",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 2,
            TargetUrl = "http://dispatcher",
            IntervalSec = 1,
            StartedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var job = new DispatcherBlockCheckJob(
            db,
            CreateSender("1"),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<DispatcherBlockCheckJob>.Instance);
        var context = new Mock<IJobExecutionContext>();
        var dataMap = new JobDataMap { { RuntimeJobKeys.EmulatorId, "em-1" } };
        context.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "em-1");
        Assert.Equal(nameof(EmulatorStatus.Error), emulator.Status);
        Assert.Equal("Протокол 2 заблокирован Dispatcher", emulator.LastError);
        Assert.Contains(await db.SystemEvents.ToListAsync(), ev =>
            ev.Level == UniEmuJson.EnumString(EventLevel.Error) &&
            ev.Message == "Протокол 2 заблокирован Dispatcher");
    }

    private static TelemetryPacketSender CreateSender(string blockedAnswer)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TelemetryPacketSender)))
            .Returns(new HttpClient(new BlockedHandler(blockedAnswer)));

        return new TelemetryPacketSender(factory.Object, NullLogger<TelemetryPacketSender>.Instance);
    }

    private sealed class BlockedHandler(string blockedAnswer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(blockedAnswer),
            });
        }
    }

    private sealed class NoopRuntimeUpdateBroadcaster : IRuntimeUpdateBroadcaster
    {
        public Task SendTelemetryAsync(RuntimeTelemetryUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendTagValueAsync(RuntimeTagValueUpdateDto update, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendEmulatorUpdatedAsync(EmulatorDto emulator, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendEventCreatedAsync(SystemEventDto ev, IReadOnlyList<string> groups, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
