using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniEmu.Runtime;

public sealed record TelemetryPacket(string EmulatorId, DateTimeOffset Timestamp, IReadOnlyDictionary<string, double> Values);
public sealed record UniversalValue(string Key, object? Value);
public sealed record UniversalPostRequest(object MachineIntegrationId, bool UseInnerId, List<UniversalValue> ListValues);
public sealed record DispatcherProgram(string Name, byte[] Bytes);
public sealed record DispatcherMonitoringAnswer(int FileType, int GetFile);

public sealed class DispatcherProtocolException(string message) : Exception(message);

public sealed class TelemetryPacketSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryPacketSender> _logger;

    public TelemetryPacketSender(IHttpClientFactory httpClientFactory, ILogger<TelemetryPacketSender> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(TelemetryPacketSender));
        _logger = logger;
    }

    private const int ProgramChunkSize = 4096;
    private static readonly JsonSerializerOptions DispatcherJsonOptions = new(JsonSerializerDefaults.General);

    public async Task<DispatcherMonitoringAnswer> SendMonitoringAsync(
        string targetUrl,
        UniversalPostRequest request,
        CancellationToken cancellationToken)
    {
        var uri = BuildDispatcherUri(targetUrl, "PostUniversalMonitoringDataJson");

        using var response = await _httpClient.PostAsJsonAsync(uri, request, DispatcherJsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Dispatcher monitoring POST to {TargetUrl} failed with {StatusCode}", uri, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var answer = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMonitoringAnswer(answer);
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
            using var response = await _httpClient.PostAsJsonAsync(uri, request, DispatcherJsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dispatcher file POST to {TargetUrl} failed with {StatusCode}", uri, response.StatusCode);
                response.EnsureSuccessStatusCode();
            }

            var answer = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureOkFileAnswer(answer, "PostFileUniversal");
        }
        while (offset < program.Bytes.Length);
    }

    public async Task<DispatcherProgram?> ReceiveProgramAsync(
        string targetUrl,
        object machineIntegrationId,
        CancellationToken cancellationToken)
    {
        var hashUri = BuildGetFileUri(targetUrl, machineIntegrationId, fileType: 1);
        var hashAnswer = await _httpClient.GetStringAsync(hashUri, cancellationToken);

        EnsureNotDispatcherError(hashAnswer, "GetFileUniversal hash");

        if (!hashAnswer.Contains("Hash=", StringComparison.OrdinalIgnoreCase))
            throw new DispatcherProtocolException($"Unexpected Dispatcher hash answer: {NormalizeAnswerForMessage(hashAnswer)}");

        var hashValue = hashAnswer[(hashAnswer.IndexOf("Hash=", StringComparison.OrdinalIgnoreCase) + 5)..].Trim();
        var expectedHash = Convert.FromBase64String(hashValue);
        using var content = new MemoryStream();

        while (true)
        {
            var chunkUri = BuildGetFileUri(targetUrl, machineIntegrationId, fileType: 0);
            var chunkAnswer = await _httpClient.GetStringAsync(chunkUri, cancellationToken);

            EnsureNotDispatcherError(chunkAnswer, "GetFileUniversal file block");

            if (chunkAnswer == "EOF")
                break;

            var bytes = Convert.FromBase64String(chunkAnswer.Trim());
            await content.WriteAsync(bytes, cancellationToken);
        }

        var receivedBytes = content.ToArray();
        var actualHash = MD5.HashData(receivedBytes);
        if (!actualHash.SequenceEqual(expectedHash))
            _logger.LogWarning("Received Dispatcher program hash mismatch for machine {MachineIntegrationId}", machineIntegrationId);

        return new DispatcherProgram($"received_program_machine_id_{machineIntegrationId}.txt", receivedBytes);
    }

    public async Task<bool> GetIsMonitoringBlockedAsync(
        string targetUrl,
        object protocolId,
        CancellationToken cancellationToken)
    {
        var uri = BuildIsMonitoringBlockedUri(targetUrl, protocolId);
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Dispatcher blocked GET to {TargetUrl} failed with {StatusCode}", uri, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var answer = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        return answer switch
        {
            "1" => true,
            "0" => false,
            _ => throw new DispatcherProtocolException($"Unexpected Dispatcher blocked answer: {NormalizeAnswerForMessage(answer)}"),
        };
    }

    public static DispatcherMonitoringAnswer ParseMonitoringAnswer(string? answer)
    {
        EnsureNotDispatcherError(answer, "PostUniversalMonitoringDataJson");

        var values = answer!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        if (!TryGetInt(values, "FileType", out var fileType) ||
            !TryGetInt(values, "GetFile", out var getFile))
        {
            throw new DispatcherProtocolException($"Unexpected Dispatcher monitoring answer: {NormalizeAnswerForMessage(answer)}");
        }

        return new DispatcherMonitoringAnswer(fileType, getFile);
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
        var protocolId = WebUtility.UrlEncode(machineIntegrationId.ToString());
        return new Uri($"{baseUri}?machine_id={protocolId}&file_type={fileType}");
    }

    private static Uri BuildIsMonitoringBlockedUri(string targetUrl, object protocolId)
    {
        var baseUri = BuildDispatcherUri(targetUrl, "GetIsMonitoringBlocked");
        var encodedProtocolId = WebUtility.UrlEncode(protocolId.ToString());
        return new Uri($"{baseUri}?machine_id={encodedProtocolId}");
    }

    private static void EnsureOkFileAnswer(string? answer, string operation)
    {
        EnsureNotDispatcherError(answer, operation);

        if (!string.Equals(answer!.Trim(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new DispatcherProtocolException($"Unexpected Dispatcher {operation} answer: {NormalizeAnswerForMessage(answer)}");
        }
    }

    private static void EnsureNotDispatcherError(string? answer, string operation)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new DispatcherProtocolException($"Empty Dispatcher {operation} answer");
        }

        if (string.Equals(answer.Trim(), "error", StringComparison.OrdinalIgnoreCase))
        {
            throw new DispatcherProtocolException($"Dispatcher {operation} returned error");
        }
    }

    private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }

    private static string NormalizeAnswerForMessage(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return "<empty>";
        }

        var trimmed = answer.Trim();
        return trimmed.Length <= 128 ? trimmed : trimmed[..128];
    }

    public static DispatcherProgram FromTextProgram(string name, string content)
    {
        return new DispatcherProgram(name, Encoding.UTF8.GetBytes(content));
    }
}
