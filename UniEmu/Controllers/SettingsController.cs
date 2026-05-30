using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniEmu.Contracts.Dtos;
using UniEmu.Hosting;

namespace UniEmu.Features.Settings;

/// <summary>
/// HTTP API для чтения настроек клиентского приложения.
/// </summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController(IOptions<UniEmuOptions> options) : ControllerBase
{
    /// <summary>
    /// Возвращает настройки, которые фронт использует как значения по умолчанию.
    /// </summary>
    /// <returns>Настройки клиентского приложения.</returns>
    [HttpGet]
    public AppSettingsDto Get()
    {
        return new AppSettingsDto(options.Value.DefaultTargetUrl);
    }
}
