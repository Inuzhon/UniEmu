using Microsoft.Extensions.Configuration;

namespace UniEmu.Hosting;

/// <summary>
/// Настройки HTTP-порта backend-приложения.
/// </summary>
/// <param name="Port">Порт, на котором должен слушать backend.</param>
public sealed record BackendPortOptions(int Port)
{
    /// <summary>
    /// Порт backend по умолчанию.
    /// </summary>
    public const int DefaultPort = 5083;

    /// <summary>
    /// HTTP-адрес привязки Kestrel для выбранного порта.
    /// </summary>
    public string HttpUrl => $"http://0.0.0.0:{Port}";

    /// <summary>
    /// Читает настройки порта из конфигурации приложения.
    /// </summary>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Разрешенные настройки порта.</returns>
    public static BackendPortOptions Resolve(IConfiguration configuration)
    {
        var port = configuration.GetValue("UniEmu:Port", DefaultPort);

        return new BackendPortOptions(port);
    }
}
