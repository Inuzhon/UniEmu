using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace UniEmu.Hosting;

public sealed record ApplicationGlobalizationOptions(string TimeZone, string Culture)
{
    public const string DefaultTimeZone = "Europe/Moscow";
    public const string DefaultCulture = "ru-RU";

    public static ApplicationGlobalizationOptions Resolve(IConfiguration configuration)
    {
        var timeZone = configuration.GetValue("UniEmu:TimeZone", DefaultTimeZone);
        var culture = configuration.GetValue("UniEmu:Culture", DefaultCulture);

        return new ApplicationGlobalizationOptions(timeZone, culture);
    }
}

public static class ApplicationGlobalization
{
    public static TimeZoneInfo CurrentTimeZone { get; private set; } = ResolveTimeZone(ApplicationGlobalizationOptions.DefaultTimeZone);

    public static DateTimeOffset ToApplicationTime(DateTimeOffset timestamp) =>
        TimeZoneInfo.ConvertTime(timestamp, CurrentTimeZone);

    public static void Apply(ApplicationGlobalizationOptions options)
    {
        var culture = CultureInfo.GetCultureInfo(options.Culture);
        CurrentTimeZone = ResolveTimeZone(options.TimeZone);

        Environment.SetEnvironmentVariable("TZ", options.TimeZone);
        TimeZoneInfo.ClearCachedData();

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
        catch (InvalidTimeZoneException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
