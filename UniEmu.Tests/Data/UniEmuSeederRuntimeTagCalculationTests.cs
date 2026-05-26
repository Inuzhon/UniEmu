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

namespace UniEmu.Tests.Data;

public sealed class UniEmuSeederRuntimeTagCalculationTests
{
    [Fact]
    public async Task BuildValuesAsync_CalculatesTypedValuesForEverySeedTag()
    {
        await using var fixture = await SeedRuntimeDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var job = CreateJob(db, dataCache, stateStore);
        var startedAt = DateTimeOffset.Parse("2026-05-24T12:00:00Z");
        var emulatorNames = await db.Emulators
            .OrderBy(emulator => emulator.Name)
            .Select(emulator => emulator.Name)
            .ToListAsync();
        var failures = new List<string>();

        foreach (var emulatorName in emulatorNames)
        {
            var emulator = await LoadRunningSeedEmulatorAsync(db, dataCache, emulatorName, startedAt);
            var values = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(75));

            if (values.Count != emulator.Tags.Count)
            {
                failures.Add($"{emulator.Name}: calculated {values.Count} values for {emulator.Tags.Count} tags.");
                continue;
            }

            foreach (var (tag, value) in emulator.Tags.Zip(values))
            {
                var failure = ValidateSeedTagValue(emulator, tag, value);
                if (failure is not null)
                {
                    failures.Add(failure);
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task BuildValuesAsync_AccumulatesCncCycleTimeFromCurrentExecutionState()
    {
        await using var fixture = await SeedRuntimeDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var job = CreateJob(db, dataCache, stateStore);
        var startedAt = DateTimeOffset.Parse("2026-05-24T12:00:00Z");
        var cncEmulatorNames = await db.Emulators
            .Where(emulator => emulator.ProtocolId >= 41 && emulator.ProtocolId <= 43)
            .OrderBy(emulator => emulator.Name)
            .Select(emulator => emulator.Name)
            .ToListAsync();

        foreach (var emulatorName in cncEmulatorNames)
        {
            var emulator = await LoadRunningSeedEmulatorAsync(db, dataCache, emulatorName, startedAt);

            _ = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(20));
            var activeValues = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(22));

            Assert.Equal("ACTIVE", activeValues.Single(value => value.Key == "ExecutionState").Value);
            Assert.Equal(2d, activeValues.Single(value => value.Key == "CycleTimeSec").Value);
        }
    }

    private static async Task<EmulatorEntity> LoadRunningSeedEmulatorAsync(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        string name,
        DateTimeOffset startedAt)
    {
        var emulator = await db.Emulators.SingleAsync(emulator => emulator.Name == name);
        emulator.Status = nameof(EmulatorStatus.Running);
        emulator.StartedAt = startedAt;
        emulator.NextRun = startedAt;
        await db.SaveChangesAsync();
        dataCache.InvalidateEmulator(emulator.Id);

        return await dataCache.GetEmulatorWithTagsAsync(emulator.Id, CancellationToken.None)
               ?? throw new InvalidOperationException($"Seed emulator '{name}' was not found.");
    }

    private static string? ValidateSeedTagValue(
        EmulatorEntity emulator,
        EmulatorTagEntity tag,
        GeneratedTagValue value)
    {
        if (value.Key != tag.Key)
        {
            return $"{emulator.Name}/{tag.Key}: calculated key '{value.Key}'.";
        }

        if (value.Name != tag.Name)
        {
            return $"{emulator.Name}/{tag.Key}: calculated name '{value.Name}'.";
        }

        SpecialParameter? expectedSpecialParameter = string.IsNullOrWhiteSpace(tag.SpecialParameter)
            ? null
            : UniEmuJson.EnumValue<SpecialParameter>(tag.SpecialParameter);
        if (value.SpecialParameter != expectedSpecialParameter)
        {
            return $"{emulator.Name}/{tag.Key}: calculated special parameter '{value.SpecialParameter}'.";
        }

        var tagType = UniEmuJson.EnumValue<TagType>(tag.Type);
        var typeFailure = tagType switch
        {
            TagType.Bool when value.Value is not bool => "bool",
            TagType.Int when value.Value is not int => "int",
            TagType.Double when value.Value is not double => "double",
            TagType.String when value.Value is not string => "string",
            _ => null,
        };

        if (typeFailure is not null)
        {
            return $"{emulator.Name}/{tag.Key}: expected {typeFailure}, got {value.Value?.GetType().Name ?? "null"}.";
        }

        if (tagType == TagType.String)
        {
            if (value.NumericValue is not null)
            {
                return $"{emulator.Name}/{tag.Key}: string tag has numeric value {value.NumericValue}.";
            }
        }
        else if (value.NumericValue is null)
        {
            return $"{emulator.Name}/{tag.Key}: numeric tag has no numeric value.";
        }

        return null;
    }

    private static EmulatorPublishJob CreateJob(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore)
    {
        return new EmulatorPublishJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            CreateSender(),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<EmulatorPublishJob>.Instance);
    }

    private static async Task<IReadOnlyList<GeneratedTagValue>> InvokeBuildValuesAsync(
        EmulatorPublishJob job,
        EmulatorEntity emulator,
        DateTimeOffset timestamp)
    {
        var method = typeof(EmulatorPublishJob).GetMethod(
            "BuildValuesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<IReadOnlyList<GeneratedTagValue>>)method.Invoke(
            job,
            [emulator, timestamp, timestamp, CancellationToken.None])!;

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

    private sealed class SeedRuntimeDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private SeedRuntimeDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<SeedRuntimeDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.MigrateAsync();
            await UniEmuSeeder.SeedAsync(db);

            return new SeedRuntimeDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
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
