using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

public sealed class EmulatorPublishJobTests
{
    [Fact]
    public async Task Execute_CalculatesDisabledTagsButDoesNotSendThemToDispatcher()
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
            Id = "emu-1",
            Name = "Main emulator",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 18,
            TargetUrl = "http://dispatcher.test",
            IntervalSec = 1,
            StartedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            Tags =
            [
                CreateTag("Temperature", "Temp", enabled: true),
                CreateGeneratorTag("tg-internal-load", "InternalLoad", "InternalLoad", enabled: false),
            ],
        });
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var handler = new CaptureMonitoringHandler();
        var job = CreateJob(db, dataCache, stateStore, CreateSender(handler));

        await job.Execute(CreateContext("emu-1"));

        Assert.NotNull(handler.MonitoringJson);
        using var document = JsonDocument.Parse(handler.MonitoringJson);
        var listValues = document.RootElement.GetProperty("ListValues").EnumerateArray().ToList();
        Assert.Contains(listValues, value => value.GetProperty("Key").GetString() == "Temp");
        Assert.DoesNotContain(listValues, value => value.GetProperty("Key").GetString() == "InternalLoad");

        var disabledTag = await db.EmulatorTags.SingleAsync(tag => tag.Id == "tg-internal-load");
        Assert.Equal("99", disabledTag.Preview);
        Assert.True(stateStore.TryGet("emu-1", "tg-internal-load", out var runtimeValue));
        Assert.Equal(99d, runtimeValue.Value);

        var telemetryPoint = await db.TelemetryPoints.SingleAsync();
        using var telemetry = JsonDocument.Parse(telemetryPoint.ValuesJson);
        Assert.Equal(99d, telemetry.RootElement.GetProperty("InternalLoad").GetDouble());
    }

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
    public void BuildTelemetryValues_IncludesStringAndNumericTagValues()
    {
        var values = new[]
        {
            new GeneratedTagValue("NumericTag", "NumericTag", 50, 50, null),
            new GeneratedTagValue("ScriptByTag", "ScriptByTag", "50", null, null),
        };

        var telemetryValues = EmulatorPublishJob.BuildTelemetryValues(values);

        Assert.Equal(50, telemetryValues["NumericTag"]);
        Assert.Equal("50", telemetryValues["ScriptByTag"]);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesFrameFromSubprogram_WhenSubprogramIsSet()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Emulators.Add(new EmulatorEntity { Id = "emu-1", Status = nameof(EmulatorStatus.Running), IntervalSec = 1 });
        db.CncPrograms.AddRange(
            CreateProgram("cnc-main", "Main.nc", CncScope.Shared, null, "MAIN0\nMAIN1\nMAIN2"),
            CreateProgram("cnc-sub", "Sub.nc", CncScope.Emulator, "emu-1", "SUB0\nSUB1\nSUB2"));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var timestamp = DateTimeOffset.Parse("2026-05-10T12:00:02Z");
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, "Main.nc", SpecialParameter.PrgName),
                CreateSpecialTag("tg-sub", "Subprogram", "Subprogram", TagType.String, TagSource.Static, "Sub.nc", SpecialParameter.Subprogram),
                CreateSpecialTag("tg-frame-num", "Frame number", "FrameNum", TagType.Int, TagSource.Static, "0", SpecialParameter.FrameNum),
                CreateSpecialTag("tg-frame-text", "Frame text", "FrameText", TagType.String, TagSource.Static, "", SpecialParameter.FrameText),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Equal(2, values.Single(value => value.SpecialParameter == SpecialParameter.FrameNum).Value);
        Assert.Equal("SUB2", values.Single(value => value.SpecialParameter == SpecialParameter.FrameText).Value);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesFrameFromMainProgram_WhenSubprogramIsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.CncPrograms.Add(CreateProgram("cnc-main", "Main.nc", CncScope.Shared, null, "MAIN0\nMAIN1\nMAIN2"));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var timestamp = DateTimeOffset.Parse("2026-05-10T12:00:01Z");
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, "Main.nc", SpecialParameter.PrgName),
                CreateSpecialTag("tg-sub", "Subprogram", "Subprogram", TagType.String, TagSource.Static, "", SpecialParameter.Subprogram),
                CreateSpecialTag("tg-frame-num", "Frame number", "FrameNum", TagType.Int, TagSource.Static, "0", SpecialParameter.FrameNum),
                CreateSpecialTag("tg-frame-text", "Frame text", "FrameText", TagType.String, TagSource.Static, "", SpecialParameter.FrameText),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Equal(1, values.Single(value => value.SpecialParameter == SpecialParameter.FrameNum).Value);
        Assert.Equal("MAIN1", values.Single(value => value.SpecialParameter == SpecialParameter.FrameText).Value);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesFrameNumberAndTextFromProgramName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.CncPrograms.Add(CreateProgram("cnc-main", "Cycle.nc", CncScope.Shared, null, "N10\r\nN20\r\nN30\r\nM30\r\n"));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var timestamp = DateTimeOffset.Parse("2026-05-10T12:00:10Z");
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 2,
            StartedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, "Cycle.nc", SpecialParameter.PrgName),
                CreateSpecialTag("tg-frame-num", "Frame number", "FrameNum", TagType.Int, TagSource.Static, "0", SpecialParameter.FrameNum),
                CreateSpecialTag("tg-frame-text", "Frame text", "FrameText", TagType.String, TagSource.Static, "", SpecialParameter.FrameText),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Equal("Cycle.nc", values.Single(value => value.SpecialParameter == SpecialParameter.PrgName).Value);
        Assert.Equal(1, values.Single(value => value.SpecialParameter == SpecialParameter.FrameNum).Value);
        Assert.Equal("N20", values.Single(value => value.SpecialParameter == SpecialParameter.FrameText).Value);
        Assert.Equal("1", emulator.Tags.Single(tag => tag.Id == "tg-frame-num").Preview);
        Assert.Equal("N20", emulator.Tags.Single(tag => tag.Id == "tg-frame-text").Preview);
    }

    [Fact]
    public async Task BuildValuesAsync_AdvancesThroughEveryFrameUntilShortProgramEnd()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.CncPrograms.Add(CreateProgram("cnc-short", "Short.nc", CncScope.Shared, null, "N10\nN20\nM30"));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var emulator = CreateFrameCalculationEmulator(startedAt, intervalSec: 1, programName: "Short.nc");
        var job = CreateJob(db, dataCache, stateStore);

        var firstFrame = await InvokeBuildValuesAsync(job, emulator, startedAt);
        var secondFrame = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(1));
        var lastFrame = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(2));

        AssertProgramFrame(firstFrame, 0, "N10");
        AssertProgramFrame(secondFrame, 1, "N20");
        AssertProgramFrame(lastFrame, 2, "M30");
    }

    [Fact]
    public async Task BuildValuesAsync_LoopsShortProgramFrameCounterAfterLastFrame()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.CncPrograms.Add(CreateProgram("cnc-short", "Short.nc", CncScope.Shared, null, "N10\nN20\nM30"));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var emulator = CreateFrameCalculationEmulator(startedAt, intervalSec: 1, programName: "Short.nc");
        var job = CreateJob(db, dataCache, stateStore);

        _ = await InvokeBuildValuesAsync(job, emulator, startedAt);
        _ = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(1));
        _ = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(2));
        var loopedFrame = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(3));

        AssertProgramFrame(loopedFrame, 0, "N10");
        Assert.Equal("0", emulator.Tags.Single(tag => tag.Id == "tg-frame-num").Preview);
        Assert.Equal("N10", emulator.Tags.Single(tag => tag.Id == "tg-frame-text").Preview);
    }

    [Fact]
    public async Task Execute_ResolvesProgramByPrgNameAndSendsMatchingContent_WhenDispatcherRequestsMainProgram()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        const string programContent = "G1 X10\nM30";
        db.Emulators.Add(new EmulatorEntity
        {
            Id = "emu-1",
            Name = "Main emulator",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 18,
            TargetUrl = "http://dispatcher.test",
            IntervalSec = 1,
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, "MAIN.NC", SpecialParameter.PrgName),
            ],
        });
        db.CncPrograms.Add(CreateProgram("cnc-main", "main.nc", CncScope.Shared, null, programContent));
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var handler = new DispatcherProgramUploadHandler();
        var job = CreateJob(db, dataCache, stateStore, CreateSender(handler));

        await job.Execute(CreateContext("emu-1"));

        Assert.Contains("\"Key\":\"PrgName\"", handler.MonitoringJson);
        Assert.Contains("\"Value\":\"MAIN.NC\"", handler.MonitoringJson);
        Assert.Equal(programContent, handler.UploadedProgramText);
        Assert.Equal(18, handler.UploadedMachineIntegrationId);
    }

    [Fact]
    public async Task HandleDispatcherAnswerAsync_StoresReceivedProgramNameInStaticPrgName()
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
        var programBytes = Encoding.UTF8.GetBytes("G1 X1\nM30");
        var sender = CreateSender(new DispatcherReceiveHandler(programBytes, machineIntegrationId: 18));
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            ProtocolId = 18,
            TargetUrl = "http://dispatcher.test",
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, "Old.nc", SpecialParameter.PrgName),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore, sender);

        await InvokeHandleDispatcherAnswerAsync(job, emulator, 18, new DispatcherMonitoringAnswer(FileType: 0, GetFile: 1));

        Assert.Equal("received_program_machine_id_18.txt", emulator.Tags.Single().Preview);
        Assert.True(stateStore.TryGet("emu-1", "tg-prg", out var runtimeValue));
        Assert.Equal("received_program_machine_id_18.txt", runtimeValue.Value);
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

    [Fact]
    public async Task BuildValuesAsync_RecalculatesScenarioTimelineOnPublish_WhenScenarioTriggerMatchesPublishInterval()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new UniEmuDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = startedAt,
            Tags =
            [
                CreatePublishIntervalScenarioTag(),
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

        var atStart = await InvokeBuildValuesAsync(job, emulator, startedAt);
        var afterFiveSeconds = await InvokeBuildValuesAsync(job, emulator, startedAt.AddSeconds(5));

        Assert.Equal(0d, atStart.Single().Value);
        Assert.Equal(50d, afterFiveSeconds.Single().Value);
        Assert.Equal("50", emulator.Tags.Single().Preview);
    }

    [Fact]
    public async Task BuildValuesAsync_UsesRuntimeStateForCronTags_WhenCronJobAlreadyCalculatedValue()
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
                    "tg-cron",
                    "CronDate",
                    TagTriggerMode.Cron,
                    null,
                    "cron-old",
                    "return \"cron-script-should-not-run\";"),
            ],
        };
        stateStore.Set(emulator.Id, "tg-cron", "CronDate", "cron-new", null, DateTimeOffset.UtcNow);
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

        var value = Assert.Single(values);
        Assert.Equal("cron-new", value.Value);
    }

    [Fact]
    public async Task BuildValuesAsync_RecalculatesPublishIntervalScriptAfterGeneratorDependencies()
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
        var scheduledAt = DateTimeOffset.Parse("2026-05-10T12:00:10Z");
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            Tags =
            [
                CreatePublishIntervalScriptTag(
                    "tg-abs",
                    "Abs",
                    """
                    if (!UniEmu.Tags.TryGetValue("NumverTag", out var numverTag))
                        return 0;

                    if (numverTag.Type != TagScriptValueType.Double)
                        return 0;

                    if (numverTag.Value is not double correctValue)
                        return 0;

                    return Math.Abs(correctValue);
                    """),
                CreatePublishIntervalGeneratorTag("tg-number", "NumverTag", "NumverTag"),
            ],
        };
        stateStore.Set(emulator.Id, "tg-abs", "Abs", 1d, 1d, scheduledAt);
        stateStore.Set(emulator.Id, "tg-number", "NumverTag", -5d, -5d, scheduledAt);
        var job = new EmulatorPublishJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            CreateSender(),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<EmulatorPublishJob>.Instance);

        var values = await InvokeBuildValuesAsync(job, emulator, scheduledAt);

        Assert.Equal(-5d, values.Single(value => value.Name == "NumverTag").Value);
        Assert.Equal(5d, values.Single(value => value.Name == "Abs").Value);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesScriptFromGeneratorTagInSamePublishSnapshot()
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
        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var timestamp = startedAt.AddSeconds(4);
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = startedAt,
            Tags =
            [
                CreateIntervalGeneratorTag(
                    "tg-generator-load",
                    "GeneratorLoad",
                    "GeneratorLoad",
                    intervalValue: 2,
                    new TagCalcConfigDto(
                        CalcType.Line,
                        Start: "10",
                        Finish: "30",
                        Duration: 8,
                        Amplitude: null,
                        Period: null,
                        Curvature: null,
                        Distortion: null)),
                CreatePublishIntervalScriptTag(
                    "tg-generator-script",
                    "GeneratorScript",
                    """
                    if (!UniEmu.Tags.TryGetValue("GeneratorLoad", out var generated) || generated?.Value is not double value)
                        return 0;

                    return value * 2 + 1;
                    """),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Equal(20d, values.Single(value => value.Name == "GeneratorLoad").Value);
        Assert.Equal(41d, values.Single(value => value.Name == "GeneratorScript").Value);
        Assert.True(stateStore.TryGet(emulator.Id, "tg-generator-load", out var generatorState));
        Assert.Equal(20d, generatorState.Value);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesScriptFromScenarioTagInSamePublishSnapshot()
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
        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var timestamp = startedAt.AddSeconds(4);
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = startedAt,
            Tags =
            [
                CreateIntervalScenarioTag(
                    "tg-scenario-pressure",
                    "ScenarioPressure",
                    "ScenarioPressure",
                    intervalValue: 2,
                    new TagScenarioConfigDto(
                    [
                        new TagScenarioSegmentDto(
                            "ramp",
                            Duration: 8,
                            new TagCalcConfigDto(
                                CalcType.Line,
                                Start: "100",
                                Finish: "140",
                                Duration: 8,
                                Amplitude: null,
                                Period: null,
                                Curvature: null,
                                Distortion: null),
                            Label: "Ramp"),
                    ],
                    ContinueOnFormulaEnd.Stretch,
                    StartValue: null)),
                CreatePublishIntervalScriptTag(
                    "tg-scenario-script",
                    "ScenarioScript",
                    """
                    if (!UniEmu.Tags.TryGetValue("ScenarioPressure", out var scenario) || scenario?.Value is not double value)
                        return 0;

                    return value / 4;
                    """),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Equal(120d, values.Single(value => value.Name == "ScenarioPressure").Value);
        Assert.Equal(30d, values.Single(value => value.Name == "ScenarioScript").Value);
        Assert.True(stateStore.TryGet(emulator.Id, "tg-scenario-pressure", out var scenarioState));
        Assert.Equal(120d, scenarioState.Value);
    }

    [Fact]
    public async Task BuildValuesAsync_CalculatesGeneratorAndScenarioDependenciesBeforeScriptsInSameSnapshot()
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
        var startedAt = DateTimeOffset.Parse("2026-05-10T12:00:00Z");
        var timestamp = startedAt.AddSeconds(4);
        var emulator = new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = 1,
            StartedAt = startedAt,
            Tags =
            [
                CreatePublishIntervalScriptTag(
                    "tg-generator-script",
                    "GeneratorScript",
                    """
                    if (!UniEmu.Tags.TryGetValue("GeneratorLoad", out var generated) || generated?.Value is not double value)
                        return 0;

                    return value * 2;
                    """),
                CreateIntervalGeneratorTag(
                    "tg-generator-load",
                    "GeneratorLoad",
                    "GeneratorLoad",
                    intervalValue: 2,
                    new TagCalcConfigDto(
                        CalcType.Line,
                        Start: "10",
                        Finish: "30",
                        Duration: 8,
                        Amplitude: null,
                        Period: null,
                        Curvature: null,
                        Distortion: null)),
                CreatePublishIntervalScriptTag(
                    "tg-scenario-script",
                    "ScenarioScript",
                    """
                    if (!UniEmu.Tags.TryGetValue("ScenarioPressure", out var scenario) || scenario?.Value is not double value)
                        return 0;

                    return value / 4;
                    """),
                CreateIntervalScenarioTag(
                    "tg-scenario-pressure",
                    "ScenarioPressure",
                    "ScenarioPressure",
                    intervalValue: 2,
                    new TagScenarioConfigDto(
                    [
                        new TagScenarioSegmentDto(
                            "ramp",
                            Duration: 8,
                            new TagCalcConfigDto(
                                CalcType.Line,
                                Start: "100",
                                Finish: "140",
                                Duration: 8,
                                Amplitude: null,
                                Period: null,
                                Curvature: null,
                                Distortion: null),
                            Label: "Ramp"),
                    ],
                    ContinueOnFormulaEnd.Stretch,
                    StartValue: null)),
            ],
        };
        var job = CreateJob(db, dataCache, stateStore);

        var values = await InvokeBuildValuesAsync(job, emulator, timestamp);

        Assert.Collection(
            values,
            value => Assert.Equal(40d, value.Value),
            value => Assert.Equal(20d, value.Value),
            value => Assert.Equal(30d, value.Value),
            value => Assert.Equal(120d, value.Value));
    }

    [Fact]
    public async Task BuildValuesAsync_DoesNotEvaluateCronScriptOnPublish_WhenRuntimeStateIsEmpty()
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
                    "tg-cron",
                    "CronDate",
                    TagTriggerMode.Cron,
                    null,
                    "cron-old",
                    "throw new InvalidOperationException(\"publish must not calculate cron\");"),
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

        var value = Assert.Single(values);
        Assert.Equal("cron-old", value.Value);
    }

    [Fact]
    public async Task Execute_StoresTagCalculationErrorOnEmulator()
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
            Id = "emu-1",
            Name = "Main emulator",
            Status = nameof(EmulatorStatus.Running),
            ProtocolId = 18,
            TargetUrl = "http://dispatcher.test",
            IntervalSec = 1,
            Tags =
            [
                CreateScriptTag(
                    "tg-throws",
                    "Throwing tag",
                    TagTriggerMode.Interval,
                    null,
                    "0",
                    "throw new InvalidOperationException(\"script boom\");"),
            ],
        });
        await db.SaveChangesAsync();

        var stateStore = new TagRuntimeStateStore();
        var dataCache = new CachedUniEmuDataService(db, new MemoryCache(new MemoryCacheOptions()));
        var job = CreateJob(db, dataCache, stateStore, CreateSender(new CaptureMonitoringHandler()));

        await job.Execute(CreateContext("emu-1"));

        var emulator = await db.Emulators.SingleAsync(e => e.Id == "emu-1");
        Assert.Contains("script boom", emulator.LastError, StringComparison.OrdinalIgnoreCase);
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

    private static EmulatorTagEntity CreateGeneratorTag(string id, string name, string key, bool enabled)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Generator),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 2, TagIntervalUnit.Sec)),
            CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(
                CalcType.Sequence,
                Start: "[99]",
                Finish: null,
                Duration: 1,
                Amplitude: null,
                Period: null,
                Curvature: null,
                Distortion: null)),
            Enabled = enabled,
        };
    }

    private static EmulatorTagEntity CreatePublishIntervalGeneratorTag(string id, string name, string key)
    {
        return CreateIntervalGeneratorTag(
            id,
            name,
            key,
            intervalValue: 1,
            new TagCalcConfigDto(
                CalcType.Sequence,
                Start: "[-5]",
                Finish: null,
                Duration: 1,
                Amplitude: null,
                Period: null,
                Curvature: null,
                Distortion: null));
    }

    private static EmulatorTagEntity CreateIntervalGeneratorTag(
        string id,
        string name,
        string key,
        int intervalValue,
        TagCalcConfigDto calc)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Generator),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, intervalValue, TagIntervalUnit.Sec)),
            CalcJson = UniEmuJson.Serialize(calc),
            Enabled = true,
        };
    }

    private static EmulatorTagEntity CreateIntervalScenarioTag(
        string id,
        string name,
        string key,
        int intervalValue,
        TagScenarioConfigDto scenario)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Scenario),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, intervalValue, TagIntervalUnit.Sec)),
            ScenarioJson = UniEmuJson.Serialize(scenario),
            Enabled = true,
        };
    }

    private static EmulatorTagEntity CreatePublishIntervalScenarioTag()
    {
        return new EmulatorTagEntity
        {
            Id = "tg-scenario",
            EmulatorId = "emu-1",
            Name = "Load",
            Key = "Load",
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Scenario),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
            ScenarioJson = UniEmuJson.Serialize(new TagScenarioConfigDto(
                [
                    new TagScenarioSegmentDto(
                        "line-up",
                        10,
                        new TagCalcConfigDto(CalcType.Line, "0", "100", 10, null, null, null, null),
                        "Line up"),
                    new TagScenarioSegmentDto(
                        "sine",
                        10,
                        new TagCalcConfigDto(CalcType.Sinusoid, "100", null, 10, 10, 0, null, null),
                        "Sine"),
                    new TagScenarioSegmentDto(
                        "line-down",
                        10,
                        new TagCalcConfigDto(CalcType.Line, "100", "0", 10, null, null, null, null),
                        "Line down"),
                ],
                ContinueOnFormulaEnd.Repeat,
                StartValue: null)),
            Enabled = true,
        };
    }

    private static EmulatorTagEntity CreatePublishIntervalScriptTag(string id, string name, string script)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = name,
            Type = UniEmuJson.EnumString(TagType.Double),
            Source = UniEmuJson.EnumString(TagSource.Script),
            Preview = "0",
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
            FormulaJson = UniEmuJson.Serialize(new TagFormulaConfigDto(null, script)),
            Enabled = true,
        };
    }

    private static CncProgramEntity CreateProgram(
        string id,
        string name,
        CncScope scope,
        string? emulatorId,
        string content)
    {
        return new CncProgramEntity
        {
            Id = id,
            Name = name,
            Scope = UniEmuJson.EnumString(scope),
            EmulatorId = emulatorId,
            Description = string.Empty,
            Content = content,
            SizeBytes = Encoding.UTF8.GetByteCount(content),
            UpdatedAt = DateTimeOffset.UtcNow,
            UploadedAt = DateTimeOffset.UtcNow,
        };
    }

    private static EmulatorTagEntity CreateSpecialTag(
        string id,
        string name,
        string key,
        TagType type,
        TagSource source,
        string preview,
        SpecialParameter specialParameter)
    {
        return new EmulatorTagEntity
        {
            Id = id,
            EmulatorId = "emu-1",
            Name = name,
            Key = key,
            Type = UniEmuJson.EnumString(type),
            Source = UniEmuJson.EnumString(source),
            Preview = preview,
            TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null)),
            SpecialParameter = UniEmuJson.EnumString(specialParameter),
            Enabled = true,
        };
    }

    private static EmulatorEntity CreateFrameCalculationEmulator(
        DateTimeOffset startedAt,
        int intervalSec,
        string programName)
    {
        return new EmulatorEntity
        {
            Id = "emu-1",
            Status = nameof(EmulatorStatus.Running),
            IntervalSec = intervalSec,
            StartedAt = startedAt,
            Tags =
            [
                CreateSpecialTag("tg-prg", "Program", "PrgName", TagType.String, TagSource.Static, programName, SpecialParameter.PrgName),
                CreateSpecialTag("tg-frame-num", "Frame number", "FrameNum", TagType.Int, TagSource.Static, "0", SpecialParameter.FrameNum),
                CreateSpecialTag("tg-frame-text", "Frame text", "FrameText", TagType.String, TagSource.Static, "", SpecialParameter.FrameText),
            ],
        };
    }

    private static void AssertProgramFrame(
        IReadOnlyList<GeneratedTagValue> values,
        int expectedNumber,
        string expectedText)
    {
        Assert.Equal(expectedNumber, values.Single(value => value.SpecialParameter == SpecialParameter.FrameNum).Value);
        Assert.Equal(expectedText, values.Single(value => value.SpecialParameter == SpecialParameter.FrameText).Value);
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

    private static EmulatorPublishJob CreateJob(
        UniEmuDbContext db,
        CachedUniEmuDataService dataCache,
        TagRuntimeStateStore stateStore,
        TelemetryPacketSender? sender = null)
    {
        return new EmulatorPublishJob(
            db,
            dataCache,
            new TelemetryValueGenerator(),
            new TagScriptExecutionService(db, dataCache, stateStore, new CompiledTagScriptCache()),
            stateStore,
            sender ?? CreateSender(),
            new RuntimeUpdateService(new NoopRuntimeUpdateBroadcaster()),
            NullLogger<EmulatorPublishJob>.Instance);
    }

    private static async Task<IReadOnlyList<GeneratedTagValue>> InvokeBuildValuesAsync(
        EmulatorPublishJob job,
        EmulatorEntity emulator,
        DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        var method = typeof(EmulatorPublishJob).GetMethod(
            "BuildValuesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<IReadOnlyList<GeneratedTagValue>>)method.Invoke(
            job,
            [emulator, now, now, CancellationToken.None])!;

        return await task;
    }

    private static async Task InvokeHandleDispatcherAnswerAsync(
        EmulatorPublishJob job,
        EmulatorEntity emulator,
        object machineIntegrationId,
        DispatcherMonitoringAnswer answer)
    {
        var method = typeof(EmulatorPublishJob).GetMethod(
            "HandleDispatcherAnswerAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method.Invoke(
            job,
            [emulator, machineIntegrationId, null, null, answer, CancellationToken.None])!;

        await task;
    }

    private static TelemetryPacketSender CreateSender(HttpMessageHandler? handler = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(TelemetryPacketSender)))
            .Returns(new HttpClient(handler ?? new HttpClientHandler()));

        return new TelemetryPacketSender(factory.Object, NullLogger<TelemetryPacketSender>.Instance);
    }

    private static IJobExecutionContext CreateContext(string emulatorId)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(c => c.MergedJobDataMap).Returns(new JobDataMap
        {
            [RuntimeJobKeys.EmulatorId] = emulatorId,
        });

        return context.Object;
    }

    private sealed class CaptureMonitoringHandler : HttpMessageHandler
    {
        public string? MonitoringJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            MonitoringJson = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("FileType=0;GetFile=0"),
            };
        }
    }

    private sealed class DispatcherReceiveHandler(byte[] programBytes, object machineIntegrationId) : HttpMessageHandler
    {
        private bool sentChunk;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri?.Query ?? string.Empty;
            if (query.Contains($"machine_id={machineIntegrationId}", StringComparison.OrdinalIgnoreCase) &&
                query.Contains("file_type=1", StringComparison.OrdinalIgnoreCase))
            {
                var hash = Convert.ToBase64String(MD5.HashData(programBytes));
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent($"Hash={hash}"),
                });
            }

            if (query.Contains($"machine_id={machineIntegrationId}", StringComparison.OrdinalIgnoreCase) &&
                query.Contains("file_type=0", StringComparison.OrdinalIgnoreCase))
            {
                var answer = sentChunk ? "EOF" : Convert.ToBase64String(programBytes);
                sentChunk = true;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(answer),
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found"),
            });
        }
    }

    private sealed class DispatcherProgramUploadHandler : HttpMessageHandler
    {
        private readonly MemoryStream uploadedProgram = new();

        public string MonitoringJson { get; private set; } = string.Empty;

        public string UploadedProgramText => Encoding.UTF8.GetString(uploadedProgram.ToArray());

        public int? UploadedMachineIntegrationId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("PostUniversalMonitoringDataJson", StringComparison.OrdinalIgnoreCase) == true)
            {
                MonitoringJson = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("FileType=1;GetFile=0"),
                };
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("PostFileUniversal", StringComparison.OrdinalIgnoreCase) == true)
            {
                var content = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(content);
                UploadedMachineIntegrationId = int.Parse(document.RootElement.GetProperty("MachineIntegrationId").GetString()!);

                foreach (var value in document.RootElement.GetProperty("ListValues").EnumerateArray())
                {
                    if (value.GetProperty("Key").GetString() == "FileUP")
                    {
                        var chunk = Convert.FromBase64String(value.GetProperty("Value").GetString()!);
                        uploadedProgram.Write(chunk);
                    }
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("ok"),
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found"),
            };
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
