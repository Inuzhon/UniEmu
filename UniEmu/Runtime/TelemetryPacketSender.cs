using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniEmu.Runtime;

/// <summary>
/// Legacy-пакет числовой телеметрии эмулятора.
/// </summary>
/// <param name="EmulatorId">Идентификатор эмулятора.</param>
/// <param name="Timestamp">Время формирования пакета.</param>
/// <param name="Values">Числовые значения по ключам тегов.</param>
public sealed record TelemetryPacket(string EmulatorId, DateTimeOffset Timestamp, IReadOnlyDictionary<string, double> Values);

/// <summary>
/// Пара ключ-значение для универсального Dispatcher-протокола.
/// </summary>
/// <param name="Key">Имя параметра Dispatcher.</param>
/// <param name="Value">Значение параметра.</param>
public sealed record UniversalValue(string Key, object? Value);

/// <summary>
/// Тело запроса универсального мониторинга Dispatcher.
/// </summary>
/// <param name="MachineIntegrationId">Идентификатор станка в Dispatcher.</param>
/// <param name="UseInnerId">Признак использования внутреннего идентификатора.</param>
/// <param name="ListValues">Список публикуемых значений.</param>
public sealed record UniversalPostRequest(object MachineIntegrationId, bool UseInnerId, List<UniversalValue> ListValues);

/// <summary>
/// CNC-программа, передаваемая через Dispatcher.
/// </summary>
/// <param name="Name">Имя файла программы.</param>
/// <param name="Bytes">Байтовое содержимое программы.</param>
public sealed record DispatcherProgram(string Name, byte[] Bytes);

/// <summary>
/// Ответ Dispatcher на публикацию мониторинга с признаками обмена CNC-файлами.
/// </summary>
/// <param name="FileType">Тип файла, который запрашивает Dispatcher.</param>
/// <param name="GetFile">Признак необходимости получить файл из UniEmu.</param>
public sealed record DispatcherMonitoringAnswer(int FileType, int GetFile);

/// <summary>
/// Ошибка формата или статуса ответа Dispatcher-протокола.
/// </summary>
/// <param name="message">Описание нарушения протокола.</param>
public sealed class DispatcherProtocolException(string message) : Exception(message);

/// <summary>
/// Отправляет мониторинг и CNC-программы в Dispatcher и разбирает ответы Dispatcher-протокола.
/// </summary>
public sealed class TelemetryPacketSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryPacketSender> _logger;

    /// <summary>
    /// Создает отправитель Dispatcher-запросов.
    /// </summary>
    /// <param name="httpClientFactory">Фабрика HTTP-клиентов.</param>
    /// <param name="logger">Логгер runtime-обмена с Dispatcher.</param>
    public TelemetryPacketSender(IHttpClientFactory httpClientFactory, ILogger<TelemetryPacketSender> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(TelemetryPacketSender));
        _logger = logger;
    }

    private const int ProgramChunkSize = 4096;
    private static readonly JsonSerializerOptions s_dispatcherJsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Отправляет пакет универсального мониторинга в Dispatcher.
    /// </summary>
    /// <param name="targetUrl">Базовый URL Dispatcher или полный URL endpoint-а.</param>
    /// <param name="request">Тело запроса мониторинга.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
    /// <returns>Разобранный ответ Dispatcher.</returns>
    public async Task<DispatcherMonitoringAnswer> SendMonitoringAsync(
        string targetUrl,
        UniversalPostRequest request,
        CancellationToken cancellationToken)
    {
        var uri = BuildDispatcherUri(targetUrl, "PostUniversalMonitoringDataJson");

        using var response = await _httpClient.PostAsJsonAsync(uri, request, s_dispatcherJsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Dispatcher monitoring POST to {TargetUrl} failed with {StatusCode}", uri, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var answer = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMonitoringAnswer(answer);
    }

    /// <summary>
    /// Передает CNC-программу в Dispatcher блоками с hash-контролем и признаком EOF.
    /// </summary>
    /// <param name="targetUrl">Базовый URL Dispatcher или полный URL endpoint-а.</param>
    /// <param name="machineIntegrationId">Идентификатор станка в Dispatcher.</param>
    /// <param name="useInnerId">Признак использования внутреннего идентификатора.</param>
    /// <param name="program">Программа для передачи; <see langword="null"/> пропускает отправку.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запросов.</param>
    /// <returns>Задача отправки программы.</returns>
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
            using var response = await _httpClient.PostAsJsonAsync(uri, request, s_dispatcherJsonOptions, cancellationToken);
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

    /// <summary>
    /// Получает CNC-программу из Dispatcher и проверяет целостность полученных блоков.
    /// </summary>
    /// <param name="targetUrl">Базовый URL Dispatcher или полный URL endpoint-а.</param>
    /// <param name="machineIntegrationId">Идентификатор станка в Dispatcher.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запросов.</param>
    /// <returns>Полученная программа или <see langword="null"/>, если протокол не вернул содержимое.</returns>
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

    /// <summary>
    /// Проверяет, заблокирован ли мониторинг для указанного протокола Dispatcher.
    /// </summary>
    /// <param name="targetUrl">Базовый URL Dispatcher или полный URL endpoint-а.</param>
    /// <param name="protocolId">Идентификатор станка или протокола.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
    /// <returns><see langword="true"/>, если Dispatcher сообщил о блокировке мониторинга.</returns>
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

    /// <summary>
    /// Разбирает ответ Dispatcher на публикацию мониторинга.
    /// </summary>
    /// <param name="answer">Текстовый ответ Dispatcher.</param>
    /// <returns>Типизированный ответ с полями <c>FileType</c> и <c>GetFile</c>.</returns>
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

    /// <summary>
    /// Создает Dispatcher-программу из UTF-8 текста.
    /// </summary>
    /// <param name="name">Имя файла программы.</param>
    /// <param name="content">Текстовое содержимое программы.</param>
    /// <returns>Программа с UTF-8 байтами.</returns>
    public static DispatcherProgram FromTextProgram(string name, string content)
    {
        return new DispatcherProgram(name, Encoding.UTF8.GetBytes(content));
    }
}
