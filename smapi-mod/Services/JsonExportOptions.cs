using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewStoryInspector.Services;

public static class JsonExportOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
