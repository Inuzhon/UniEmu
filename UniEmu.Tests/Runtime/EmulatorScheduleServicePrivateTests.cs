using System.Reflection;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Hosting;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class EmulatorScheduleServicePrivateTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("*/5 * * * *", "0 */5 * * * ?")]
    [InlineData("0 12 * * MON", "0 0 12 ? * MON")]
    [InlineData("0 12 1 * *", "0 0 12 1 * ?")]
    [InlineData("0 0 12 ? * MON", "0 0 12 ? * MON")]
    [InlineData("0 0 12 ? * MON 2026", "0 0 12 ? * MON 2026")]
    [InlineData("0 12 * *", null)]
    public void NormalizeCron_ConvertsUnixCronToQuartzCron(string? cron, string? expected)
    {
        Assert.Equal(expected, InvokePrivateStaticNullable<string>("NormalizeCron", cron));
    }

    [Theory]
    [InlineData(0, TagIntervalUnit.Ms, 1)]
    [InlineData(5, TagIntervalUnit.Ms, 5)]
    [InlineData(2, TagIntervalUnit.Sec, 2_000)]
    [InlineData(3, TagIntervalUnit.Min, 180_000)]
    public void ToTimeSpan_ClampsIntervalValueAndAppliesUnit(int value, TagIntervalUnit unit, double expectedMilliseconds)
    {
        var trigger = new TagTriggerDto(TagTriggerMode.Interval, null, null, value, unit);

        var interval = InvokePrivateStatic<TimeSpan>("ToTimeSpan", trigger);

        Assert.Equal(expectedMilliseconds, interval.TotalMilliseconds);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(7, 7)]
    [InlineData(30, 10)]
    public void GetDispatcherBlockCheckInterval_ClampsConfiguredSeconds(int configuredSeconds, int expectedSeconds)
    {
        var options = new UniEmuOptions { DispatcherBlockCheckIntervalSeconds = configuredSeconds };

        var interval = InvokePrivateStatic<TimeSpan>("GetDispatcherBlockCheckInterval", options);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), interval);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        return Assert.IsType<T>(InvokePrivateStaticRaw(methodName, args));
    }

    private static T? InvokePrivateStaticNullable<T>(string methodName, params object?[] args)
        where T : class
    {
        var value = InvokePrivateStaticRaw(methodName, args);
        return value is null ? null : Assert.IsType<T>(value);
    }

    private static object? InvokePrivateStaticRaw(string methodName, params object?[] args)
    {
        var method = typeof(EmulatorScheduleService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return method.Invoke(null, args);
    }
}
