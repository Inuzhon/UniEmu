using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniEmu.Common;

/// <summary>
/// Единые настройки JSON-сериализации UniEmu для API, конфигурации тегов и runtime-состояния.
/// </summary>
public static class UniEmuJson
{
    /// <summary>
    /// Общие настройки сериализации с web-именованием и строковым представлением enum.
    /// </summary>
    public static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    // Persisted tag configs may come from older versions with omitted optional constructor members.
    private static readonly JsonSerializerOptions s_deserializeOptions = CreateDeserializeOptions();

    /// <summary>
    /// Добавляет в переданные настройки обязательные для UniEmu JSON-конвертеры.
    /// </summary>
    /// <param name="options">Настройки сериализации, которые нужно дополнить.</param>
    public static void Apply(JsonSerializerOptions options)
    {
        options.AllowDuplicateProperties = false;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }

    /// <summary>
    /// Сериализует значение с общими настройками UniEmu.
    /// </summary>
    /// <typeparam name="T">Тип сериализуемого значения.</typeparam>
    /// <param name="value">Значение для сериализации.</param>
    /// <returns>JSON-строка.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, s_options);

    /// <summary>
    /// Десериализует JSON-строку с общими настройками UniEmu.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип результата.</typeparam>
    /// <param name="value">JSON-строка или пустое значение.</param>
    /// <returns>Десериализованное значение либо значение по умолчанию для пустой строки.</returns>
    public static T? Deserialize<T>(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value, s_deserializeOptions);
    }

    /// <summary>
    /// Возвращает строковое JSON-представление значения enum без кавычек.
    /// </summary>
    /// <typeparam name="T">Тип enum.</typeparam>
    /// <param name="value">Значение enum.</param>
    /// <returns>Строковое имя значения в формате API.</returns>
    public static string EnumString<T>(T value)
        where T : struct, Enum
    {
        return Serialize(value).Trim('"');
    }

    /// <summary>
    /// Преобразует строковое имя enum из API или базы данных в типизированное значение.
    /// </summary>
    /// <typeparam name="T">Тип enum.</typeparam>
    /// <param name="value">Строковое имя значения.</param>
    /// <returns>Типизированное значение enum.</returns>
    public static T EnumValue<T>(string value)
        where T : struct, Enum
    {
        return JsonSerializer.Deserialize<T>($"\"{value}\"", s_options);
    }

    private static JsonSerializerOptions CreateDeserializeOptions()
    {
        return new JsonSerializerOptions(s_options)
        {
            RespectRequiredConstructorParameters = false,
        };
    }
}
