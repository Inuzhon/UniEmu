using Microsoft.Extensions.Configuration;

namespace UniEmu.Hosting;

public sealed record BackendPortOptions(int Port)
{
    public const int DefaultPort = 5083;

    public string HttpUrl => $"http://0.0.0.0:{Port}";

    public static BackendPortOptions Resolve(IConfiguration configuration)
    {
        var port = configuration.GetValue("UniEmu:Port", DefaultPort);

        return new BackendPortOptions(port);
    }
}
