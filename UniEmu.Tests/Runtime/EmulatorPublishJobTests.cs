using UniEmu.Common;
using UniEmu.Contracts.Enums;
using UniEmu.Domain.Entities;
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
}
