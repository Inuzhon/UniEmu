using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class EmulatorPublishJobTests
{
    [Fact]
    public void BuildDispatcherValues_ExcludesDisabledTags()
    {
        var tags = new[]
        {
            CreateTag("Temperature", "Temp", enabled: true),
            CreateTag("InternalLoad", "InternalLoad", enabled: false),
        };
        var values = new[]
        {
            new GeneratedTagValue("Temp", "Temperature", 42.5, 42.5, null),
            new GeneratedTagValue("InternalLoad", "InternalLoad", 99.9, 99.9, null),
        };

        var dispatcherValues = EmulatorPublishJob.BuildDispatcherValues(tags, values);

        var value = Assert.Single(dispatcherValues);
        Assert.Equal("Temp", value.Key);
        Assert.Equal(42.5, value.Value);
    }

    [Fact]
    public async Task BuildValuesAsync_UsesPersistedPreviewForEventAndCronTags_WhenRuntimeStateIsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            Tags =
            [
                CreateScriptTag(
                    "tg-start",
                    "StartDate",
                    TagTriggerMode.Once,
                    TagTriggerEvent.OnStart,
                    "start-old",
                    "return \"start-new\";"),
                CreateScriptTag(
                    "tg-stop",
                    "StopDate",
                    TagTriggerMode.Once,
                    TagTriggerEvent.OnStop,
                    "stop-old",
                    "return \"stop-new\";"),
                CreateScriptTag(
                    "tg-cron",
                    "CronDate",
                    TagTriggerMode.Cron,
                    null,
                    "cron-old",
                    "return \"cron-new\";"),
            ],
        };
        var job = new EmulatorPublishJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            CreateSender(),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<EmulatorPublishJob>.Instance);

        var values = await InvokeBuildValuesAsync(job, emulator);

        Assert.Collection(
            values,
            value => Assert.Equal("start-old", value.Value),
            value => Assert.Equal("stop-old", value.Value),
            value => Assert.Equal("cron-old", value.Value));
    }

    private static EmulatorTagEntity CreateTag(string name, string key, bool enabled)
    {
        return new EmulatorTagEntity
        {
            Id = $"{key}-id",
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Static),
            Preview = "0",
            Enabled = enabled,
        };
    }

    private static EmulatorTagEntity CreateScriptTag(
        string id,
        string name,
        TagTriggerMode mode,
        TagTriggerEvent? ev,
        string preview,
        string script)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = name,
            Type = UniEmuJson.EnumString(TagType.String),
            Source = UniEmuJson.EnumString(TagSource.Script),
            Preview = preview,
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(mode, ev, mode == TagTriggerMode.Cron ? "0 0 * * *" : null, null, null)),
            FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            Enabled = true,
        };
    }

    private static async Task<IReadOnlyList<GeneratedTagValue>> InvokeBuildValuesAsync(
        EmulatorPublishJob job,
        EmulatorEntity emulator)
    {
        var method = typeof(EmulatorPublishJob).GetMethod(
            "BuildValuesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<IReadOnlyList<GeneratedTagValue>>)method.Invoke(
            job,
            [emulator, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, CancellationToken.None])!;

        return await task;
    }

    private static TelemetryPacketSender CreateSender()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TelemetryPacketSender)))
            .Returns(new HttpClient());

        return new TelemetryPacketSender(factory.Object, NullLogger<TelemetryPacketSender>.Instance);
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
