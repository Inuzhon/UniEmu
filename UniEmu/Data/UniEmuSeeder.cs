using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

public static class UniEmuSeeder
{
    public static async Task SeedAsync(UniEmuDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Emulators.Any())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var emulators = new[]
        {
            new EmulatorEntity
            {
                Id = "em-001",
                Name = "CNC_Mill_01",
                Status = nameof(EmulatorStatus.Stopped),
                TargetUrl = "https://scada.local/api/ingest",
                IntervalSec = 5,
                TotalRequests = 16842,
            },
            new EmulatorEntity
            {
                Id = "em-002",
                Name = "CNC_Lathe_02",
                Status = nameof(EmulatorStatus.Stopped),
                TargetUrl = "https://scada.local/api/ingest",
                IntervalSec = 2,
                TotalRequests = 64120,
            },
        };

        db.Emulators.AddRange(emulators);

        db.EmulatorTags.AddRange(
            new EmulatorTagEntity
            {
                Id = "tg-spindle",
                EmulatorId = "em-001",
                Name = "SpindleRPM",
                Key = "Custom",
                Type = UniEmuJson.EnumString(TagType.Int),
                Source = UniEmuJson.EnumString(TagSource.Generator),
                Preview = "8420",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 500, TagIntervalUnit.Ms)),
                CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(CalcType.Sinusoid, "8000", null, null, 400, 10, null, null)),
            },
            new EmulatorTagEntity
            {
                Id = "tg-temperature",
                EmulatorId = "em-001",
                Name = "Temperature",
                Key = "Custom",
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Generator),
                Preview = "45.6",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
                CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(CalcType.Line, "20", "75", 60, null, null, null, null)),
            },
            new EmulatorTagEntity
            {
                Id = "tg-feed",
                EmulatorId = "em-002",
                Name = "FeedRate",
                Key = "FeedOvr",
                Type = UniEmuJson.EnumString(TagType.Double),
                Source = UniEmuJson.EnumString(TagSource.Generator),
                Preview = "1200",
                TriggerJson = UniEmuJson.Serialize(new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec)),
                CalcJson = UniEmuJson.Serialize(new TagCalcConfigDto(CalcType.Random, "1000", "1500", null, null, null, null, null)),
            });

        db.ScriptFiles.Add(new ScriptFileEntity
        {
            Id = "scr-shared-1",
            Name = "shared/utils.csx",
            Scope = UniEmuJson.EnumString(ScriptScope.Shared),
            Content = "// shared/utils.csx\npublic static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);\n",
            UpdatedAt = now.AddHours(-1),
            SizeBytes = 120,
        });

        db.CncPrograms.Add(new CncProgramEntity
        {
            Id = "cnc-shared-1",
            Name = "PREAMBLE.nc",
            Scope = UniEmuJson.EnumString(CncScope.Shared),
            Description = "Инициализация: метрические единицы, абсолютные координаты, G54",
            Content = "; PREAMBLE\nG21 G90 G17\nG54\nG40 G49 G80\n",
            SizeBytes = 50,
            UpdatedAt = now.AddDays(-2),
            UploadedAt = now.AddDays(-14),
        });

        db.SystemEvents.Add(new SystemEventEntity
        {
            Id = "ev-seed-1",
            EmulatorId = "em-001",
            EmulatorName = "CNC_Mill_01",
            Level = UniEmuJson.EnumString(EventLevel.Info),
            Message = "Backend seed инициализирован",
            Timestamp = now,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
