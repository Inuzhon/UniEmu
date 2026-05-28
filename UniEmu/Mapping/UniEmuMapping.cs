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
    extension(EmulatorEntity entity)
    {
        /// <summary>
        /// Преобразует эмулятор в DTO со счетчиком тегов и рассчитанным uptime.
        /// </summary>
        /// <param name="tagsCount">Количество тегов эмулятора.</param>
        /// <returns>DTO эмулятора.</returns>
        public EmulatorDto ToDto(int tagsCount)
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
    }

    extension(EmulatorTagEntity entity)
    {
        /// <summary>
        /// Преобразует тег эмулятора в DTO с десериализованными настройками.
        /// </summary>
        /// <returns>DTO тега.</returns>
        public EmulatorTagDto ToDto()
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
    }

    extension(ScriptFileEntity entity)
    {
        /// <summary>
        /// Преобразует CSX-скрипт в DTO.
        /// </summary>
        /// <returns>DTO скрипта.</returns>
        public ScriptFileDto ToDto()
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
    }

    extension(CncProgramEntity entity)
    {
        /// <summary>
        /// Преобразует CNC-программу в DTO.
        /// </summary>
        /// <returns>DTO CNC-программы.</returns>
        public CncProgramDto ToDto()
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
    }

    extension(TelemetryPointEntity entity)
    {
        /// <summary>
        /// Преобразует точку телеметрии в DTO с десериализованным словарем значений.
        /// </summary>
        /// <returns>DTO точки телеметрии.</returns>
        public TelemetryPointDto ToDto()
        {
            return new TelemetryPointDto(
                entity.Timestamp,
                UniEmuJson.Deserialize<Dictionary<string, object?>>(entity.ValuesJson) ?? new Dictionary<string, object?>());
        }
    }

    extension(SystemEventEntity entity)
    {
        /// <summary>
        /// Преобразует системное событие в DTO.
        /// </summary>
        /// <returns>DTO системного события.</returns>
        public SystemEventDto ToDto()
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
}
