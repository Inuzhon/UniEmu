using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniEmu.Common;

public static class UniEmuJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Apply(JsonSerializerOptions options)
    {
        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value, Options);
    }

    public static string EnumString<T>(T value)
        where T : struct, Enum
    {
        return Serialize(value).Trim('"');
    }

    public static T EnumValue<T>(string value)
        where T : struct, Enum
    {
        return JsonSerializer.Deserialize<T>($"\"{value}\"", Options);
    }
}
