using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace UniEmu.Hosting;

/// <summary>
/// Настройки локали и часового пояса приложения.
/// </summary>
/// <param name="TimeZone">Идентификатор часового пояса приложения.</param>
/// <param name="Culture">Идентификатор культуры .NET.</param>
public sealed record ApplicationGlobalizationOptions(string TimeZone, string Culture)
{
    /// <summary>
    /// Часовой пояс приложения по умолчанию.
    /// </summary>
    public const string DefaultTimeZone = "Europe/Moscow";

    /// <summary>
    /// Культура приложения по умолчанию.
    /// </summary>
    public const string DefaultCulture = "ru-RU";

    /// <summary>
    /// Читает настройки локали и часового пояса из конфигурации.
    /// </summary>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Разрешенные настройки локализации.</returns>
    public static ApplicationGlobalizationOptions Resolve(IConfiguration configuration)
    {
        var timeZone = configuration.GetValue("UniEmu:TimeZone", DefaultTimeZone);
        var culture = configuration.GetValue("UniEmu:Culture", DefaultCulture);

        return new ApplicationGlobalizationOptions(timeZone, culture);
    }
}

/// <summary>
/// Настраивает культуру и часовой пояс процесса backend-приложения.
/// </summary>
public static class ApplicationGlobalization
{
    /// <summary>
    /// Текущий часовой пояс приложения.
    /// </summary>
    public static TimeZoneInfo CurrentTimeZone { get; private set; } = ResolveTimeZone(ApplicationGlobalizationOptions.DefaultTimeZone);

    /// <summary>
    /// Переводит временную метку в текущий часовой пояс приложения.
    /// </summary>
    /// <param name="timestamp">Исходная временная метка.</param>
    /// <returns>Временная метка в часовом поясе приложения.</returns>
    public static DateTimeOffset ToApplicationTime(DateTimeOffset timestamp) =>
        TimeZoneInfo.ConvertTime(timestamp, CurrentTimeZone);

    /// <summary>
    /// Применяет культуру и часовой пояс приложения к текущему процессу.
    /// </summary>
    /// <param name="options">Настройки локализации.</param>
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
