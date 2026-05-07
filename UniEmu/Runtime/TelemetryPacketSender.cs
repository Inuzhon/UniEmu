namespace UniEmu.Runtime;

public sealed record TelemetryPacket(string EmulatorId, DateTimeOffset Timestamp, IReadOnlyDictionary<string, double> Values);

public sealed class TelemetryPacketSender(HttpClient httpClient, ILogger<TelemetryPacketSender> logger)
{
    public async Task SendAsync(string targetUrl, TelemetryPacket packet, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid targetUrl: {targetUrl}");
        }

        using var response = await httpClient.PostAsJsonAsync(uri, packet, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telemetry POST to {TargetUrl} failed with {StatusCode}", targetUrl, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
    }
}
