using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniEmu.Runtime;

public sealed record TelemetryPacket(string EmulatorId, DateTimeOffset Timestamp, IReadOnlyDictionary<string, double> Values);
public sealed record UniversalValue(string Key, object? Value);
public sealed record UniversalPostRequest(object MachineIntegrationId, bool UseInnerId, List<UniversalValue> ListValues);
public sealed record DispatcherProgram(string Name, byte[] Bytes);

public sealed class TelemetryPacketSender(HttpClient httpClient, ILogger<TelemetryPacketSender> logger)
{
    private const int ProgramChunkSize = 4096;
    private static readonly JsonSerializerOptions DispatcherJsonOptions = new(JsonSerializerDefaults.General);

    public async Task<string> SendMonitoringAsync(
        string targetUrl,
        UniversalPostRequest request,
        CancellationToken cancellationToken)
    {
        var uri = BuildDispatcherUri(targetUrl, "PostUniversalMonitoringDataJson");

        using var response = await httpClient.PostAsJsonAsync(uri, request, DispatcherJsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Dispatcher monitoring POST to {TargetUrl} failed with {StatusCode}", uri, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task SendProgramAsync(
        string targetUrl,
        object machineIntegrationId,
        bool useInnerId,
        DispatcherProgram? program,
        CancellationToken cancellationToken)
    {
        if (program is null)
        {
            return;
        }

        var uri = BuildDispatcherUri(targetUrl, "PostFileUniversal");
        var hash = Convert.ToBase64String(MD5.HashData(program.Bytes));
        var offset = 0;
        var isFirstBlock = true;

        do
        {
            var chunkLength = Math.Min(ProgramChunkSize, program.Bytes.Length - offset);
            var chunk = program.Bytes.AsSpan(offset, chunkLength).ToArray();
            offset += chunkLength;
            var isEof = offset >= program.Bytes.Length;
            var values = new List<UniversalValue>();

            if (isFirstBlock)
            {
                values.Add(new UniversalValue("Hash", hash));
                isFirstBlock = false;
            }

            values.Add(new UniversalValue("FileUP", Convert.ToBase64String(chunk)));

            if (isEof)
            {
                values.Add(new UniversalValue("EOF", "1"));
            }

            var request = new UniversalPostRequest(machineIntegrationId.ToString() ?? string.Empty, useInnerId, values);
            using var response = await httpClient.PostAsJsonAsync(uri, request, DispatcherJsonOptions, cancellationToken);
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        while (offset < program.Bytes.Length);
    }

    public async Task<DispatcherProgram?> ReceiveProgramAsync(
        string targetUrl,
        object machineIntegrationId,
        CancellationToken cancellationToken)
    {
        var hashUri = BuildGetFileUri(targetUrl, machineIntegrationId, fileType: 1);
        var hashAnswer = await httpClient.GetStringAsync(hashUri, cancellationToken);
        if (!hashAnswer.Contains("Hash=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var expectedHash = Convert.FromBase64String(hashAnswer[(hashAnswer.IndexOf("Hash=", StringComparison.OrdinalIgnoreCase) + 5)..]);
        using var content = new MemoryStream();

        while (true)
        {
            var chunkUri = BuildGetFileUri(targetUrl, machineIntegrationId, fileType: 0);
            var chunkAnswer = await httpClient.GetStringAsync(chunkUri, cancellationToken);
            if (chunkAnswer == "EOF")
            {
                break;
            }

            var bytes = Convert.FromBase64String(chunkAnswer);
            await content.WriteAsync(bytes, cancellationToken);
        }

        var receivedBytes = content.ToArray();
        var actualHash = MD5.HashData(receivedBytes);
        if (!actualHash.SequenceEqual(expectedHash))
        {
            logger.LogWarning("Received Dispatcher program hash mismatch for machine {MachineIntegrationId}", machineIntegrationId);
        }

        return new DispatcherProgram($"received_program_machine_id_{machineIntegrationId}.txt", receivedBytes);
    }

    private static Uri BuildDispatcherUri(string targetUrl, string endpoint)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid targetUrl: {targetUrl}");
        }

        if (uri.AbsolutePath.EndsWith(endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return new Uri($"{uri.GetLeftPart(UriPartial.Authority)}/IndustryManagment/WebIntegration/{endpoint}");
    }

    private static Uri BuildGetFileUri(string targetUrl, object machineIntegrationId, int fileType)
    {
        var baseUri = BuildDispatcherUri(targetUrl, "GetFileUniversal");
        var machineId = WebUtility.UrlEncode(machineIntegrationId.ToString());
        return new Uri($"{baseUri}?machine_id={machineId}&file_type={fileType}");
    }

    public static DispatcherProgram FromTextProgram(string name, string content)
    {
        return new DispatcherProgram(name, Encoding.UTF8.GetBytes(content));
    }
}
