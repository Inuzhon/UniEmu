using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;

namespace UniEmu.Tests.Common;

public sealed class TagTriggerNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsScenarioOnStartOnceTriggerToOneSecondInterval()
    {
        var trigger = new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);

        var normalized = TagTriggerNormalizer.Normalize(TagSource.Scenario, trigger);

        Assert.Equal(TagTriggerMode.Interval, normalized.Mode);
        Assert.Null(normalized.Event);
        Assert.Null(normalized.Cron);
        Assert.Equal(1, normalized.IntervalValue);
        Assert.Equal(TagIntervalUnit.Sec, normalized.IntervalUnit);
    }

    [Fact]
    public void Normalize_LeavesNonScenarioTriggerAsIs()
    {
        var trigger = new TagTriggerDto(TagTriggerMode.Once, TagTriggerEvent.OnStart, null, null, null);

        var normalized = TagTriggerNormalizer.Normalize(TagSource.Static, trigger);

        Assert.Same(trigger, normalized);
    }
}
