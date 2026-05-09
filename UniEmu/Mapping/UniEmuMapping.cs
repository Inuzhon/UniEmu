using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Mapping;

public static class UniEmuMapping
{
    public static EmulatorDto ToDto(this EmulatorEntity entity, int tagsCount)
    {
        var uptime = entity.Status == nameof(EmulatorStatus.Running) && entity.StartedAt is not null
            ? Math.Max(0, (long)(DateTimeOffset.UtcNow - entity.StartedAt.Value).TotalSeconds)
            : 0;

        return new EmulatorDto(
            entity.Id,
            entity.Name,
            UniEmuJson.EnumValue<EmulatorStatus>(entity.Status),
            entity.ProtocolId,
            entity.TargetUrl,
            entity.IntervalSec,
            entity.LastRun,
            entity.NextRun,
            entity.LastError,
            tagsCount,
            uptime,
            entity.TotalRequests);
    }

    public static EmulatorTagDto ToDto(this EmulatorTagEntity entity)
    {
        return new EmulatorTagDto(
            entity.Id,
            entity.Name,
            entity.Key,
            UniEmuJson.EnumValue<TagType>(entity.Type),
            UniEmuJson.EnumValue<TagSource>(entity.Source),
            entity.Preview,
            UniEmuJson.Deserialize<TagTriggerDto>(entity.TriggerJson) ?? new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null),
            UniEmuJson.Deserialize<TagCalcConfigDto>(entity.CalcJson),
            UniEmuJson.Deserialize<TagFormulaConfigDto>(entity.FormulaJson),
            UniEmuJson.Deserialize<TagScenarioConfigDto>(entity.ScenarioJson),
            entity.Enabled,
            entity.RoundDigits,
            string.IsNullOrWhiteSpace(entity.SpecialParameter) ? null : UniEmuJson.EnumValue<SpecialParameter>(entity.SpecialParameter),
            entity.Description);
    }

    public static ScriptFileDto ToDto(this ScriptFileEntity entity)
    {
        return new ScriptFileDto(
            entity.Id,
            entity.Name,
            UniEmuJson.EnumValue<ScriptScope>(entity.Scope),
            entity.EmulatorId,
            entity.Content,
            entity.UpdatedAt,
            entity.SizeBytes);
    }

    public static CncProgramDto ToDto(this CncProgramEntity entity)
    {
        return new CncProgramDto(
            entity.Id,
            entity.Name,
            UniEmuJson.EnumValue<CncScope>(entity.Scope),
            entity.EmulatorId,
            entity.Description,
            entity.Content,
            entity.SizeBytes,
            entity.UpdatedAt,
            entity.UploadedAt,
            entity.IsBinary);
    }

    public static TelemetryPointDto ToDto(this TelemetryPointEntity entity)
    {
        return new TelemetryPointDto(
            entity.Timestamp,
            UniEmuJson.Deserialize<Dictionary<string, double>>(entity.ValuesJson) ?? new Dictionary<string, double>());
    }

    public static SystemEventDto ToDto(this SystemEventEntity entity)
    {
        return new SystemEventDto(
            entity.Id,
            entity.EmulatorId,
            entity.EmulatorName,
            UniEmuJson.EnumValue<EventLevel>(entity.Level),
            entity.Message,
            entity.Timestamp);
    }
}
