using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;

namespace UniEmu.Common;

/// <summary>
/// Нормализует конфигурации триггеров тегов, которые небезопасны для планирования во время выполнения.
/// </summary>
public static class TagTriggerNormalizer
{
    public static TagTriggerDto Normalize(TagSource source, TagTriggerDto trigger)
    {
        if (source == TagSource.Scenario
            && trigger.Mode == TagTriggerMode.Once
            && (trigger.Event ?? TagTriggerEvent.OnStart) == TagTriggerEvent.OnStart)
        {
            return new TagTriggerDto(TagTriggerMode.Interval, null, null, 1, TagIntervalUnit.Sec);
        }

        return trigger;
    }
}
