using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;

namespace UniEmu.Mapping;

/// <summary>
/// Преобразует доменные сущности UniEmu в DTO-контракты REST API и realtime-сообщений.
/// </summary>
public static class UniEmuMapping
{
    /// <summary>
    /// Преобразует эмулятор в DTO со счетчиком тегов и рассчитанным uptime.
    /// </summary>
    /// <param name="entity">Сущность эмулятора.</param>
    /// <param name="tagsCount">Количество тегов эмулятора.</param>
    /// <returns>DTO эмулятора.</returns>
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

    /// <summary>
    /// Преобразует тег эмулятора в DTO с десериализованными настройками.
    /// </summary>
    /// <param name="entity">Сущность тега.</param>
    /// <returns>DTO тега.</returns>
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

    /// <summary>
    /// Преобразует CSX-скрипт в DTO.
    /// </summary>
    /// <param name="entity">Сущность скрипта.</param>
    /// <returns>DTO скрипта.</returns>
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

    /// <summary>
    /// Преобразует CNC-программу в DTO.
    /// </summary>
    /// <param name="entity">Сущность CNC-программы.</param>
    /// <returns>DTO CNC-программы.</returns>
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

    /// <summary>
    /// Преобразует точку телеметрии в DTO с десериализованным словарем значений.
    /// </summary>
    /// <param name="entity">Сущность точки телеметрии.</param>
    /// <returns>DTO точки телеметрии.</returns>
    public static TelemetryPointDto ToDto(this TelemetryPointEntity entity)
    {
        return new TelemetryPointDto(
            entity.Timestamp,
            UniEmuJson.Deserialize<Dictionary<string, object?>>(entity.ValuesJson) ?? new Dictionary<string, object?>());
    }

    /// <summary>
    /// Преобразует системное событие в DTO.
    /// </summary>
    /// <param name="entity">Сущность события.</param>
    /// <returns>DTO системного события.</returns>
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
