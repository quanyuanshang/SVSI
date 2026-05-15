using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class ModScanner
{
    private const string ContentPatcherUniqueId = "Pathoschild.ContentPatcher";

    private readonly IMonitor monitor;

    private sealed class ContentJsonReadResult
    {
        public JsonNode? ContentJson { get; init; }

        public string ReadMode { get; init; } = "missing";

        public long Size { get; init; }

        public string Sha256 { get; init; } = string.Empty;

        public string? ParseError { get; init; }
    }

    public ModScanner(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    public ModScanReport Scan(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
        {
            return new ModScanReport
            {
                ModsDirectory = modsDirectory,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                ScanError = $"Mods directory not found: {modsDirectory}"
            };
        }

        var scannedMods = Directory
            .EnumerateFiles(modsDirectory, "manifest.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(this.ScanManifest)
            .ToList();

        return new ModScanReport
        {
            ModsDirectory = modsDirectory,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Mods = scannedMods
        };
    }

    private ScannedMod ScanManifest(string manifestPath)
    {
        var modDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;

        try
        {
            var root = LooseJsonParser.ParseTokenFromFile(manifestPath) as JObject
                ?? throw new InvalidOperationException("Manifest root must be a JSON object.");

            var contentPackFor = this.ReadContentPackFor(root);
            var isContentPatcherContentPack = string.Equals(
                contentPackFor?.UniqueID,
                ContentPatcherUniqueId,
                StringComparison.OrdinalIgnoreCase
            );

            string? contentJsonPath = null;
            ContentJsonReadResult? contentJsonResult = null;
            
            var configPath = Path.Combine(modDirectory, "config.json");
            var configValues = File.Exists(configPath)
                ? this.ReadConfigValues(configPath)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (isContentPatcherContentPack)
            {
                contentJsonPath = Path.Combine(modDirectory, "content.json");

                if (File.Exists(contentJsonPath))
                {
                    contentJsonResult = this.ReadContentJson(contentJsonPath);
                }
                else
                {
                    this.monitor.Log(
                        $"Content Patcher pack is missing content.json: {contentJsonPath}",
                        LogLevel.Warn
                    );
                }
            }

            return new ScannedMod
            {
                DirectoryPath = modDirectory,
                ManifestPath = manifestPath,
                Name = this.ReadString(root, "Name"),
                UniqueID = this.ReadString(root, "UniqueID"),
                Author = this.ReadString(root, "Author"),
                Version = this.ReadString(root, "Version"),
                ContentPackFor = contentPackFor,
                IsContentPatcherContentPack = isContentPatcherContentPack,
                ContentJsonPath = contentJsonPath,
                ContentJson = contentJsonResult?.ContentJson,
                ContentJsonReadMode = contentJsonResult?.ReadMode,
                ContentJsonSize = contentJsonResult?.Size,
                ContentJsonSha256 = contentJsonResult?.Sha256,
                ContentJsonError = contentJsonResult?.ParseError,
                ConfigPath = File.Exists(configPath) ? configPath : null,
                ConfigValues = configValues,
            };
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed to scan manifest '{manifestPath}': {ex.Message}", LogLevel.Warn);

            return new ScannedMod
            {
                DirectoryPath = modDirectory,
                ManifestPath = manifestPath,
                ScanError = ex.Message
            };
        }
    }

    private ContentJsonReadResult ReadContentJson(string filePath)
    {
        var rawJson = File.ReadAllText(filePath);
        var size = new FileInfo(filePath).Length;
        var sha256 = LooseJsonParser.ComputeSha256(rawJson);

        try
        {
            var contentJson = LooseJsonParser.ParseNodeFromText(rawJson);

            return new ContentJsonReadResult
            {
                ContentJson = contentJson,
                ReadMode = "parsed",
                Size = size,
                Sha256 = sha256
            };
        }
        catch (Exception ex)
        {
            return new ContentJsonReadResult
            {
                ContentJson = null,
                ReadMode = "raw-fallback",
                Size = size,
                Sha256 = sha256,
                ParseError = ex.Message
            };
        }
    }

    private Dictionary<string, string> ReadConfigValues(string filePath)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    try
    {
        if (LooseJsonParser.ParseNodeFromFile(filePath) is not JsonObject root)
        {
            return values;
        }

        foreach (var pair in root)
        {
            if (pair.Value is null)
            {
                continue;
            }

            values[pair.Key] = ReadConditionValue(pair.Value);
        }
    }
    catch (Exception ex)
    {
        this.monitor.Log($"Failed to read config.json '{filePath}': {ex.Message}", LogLevel.Warn);
    }

    return values;
}

    private static string ReadConditionValue(JsonNode? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (value is JsonValue boolValue && boolValue.TryGetValue<bool>(out var boolResult))
        {
            return boolResult ? "true" : "false";
        }

        return value.ToJsonString();
    }

    private ContentPackReference? ReadContentPackFor(JObject root)
    {
        if (root["ContentPackFor"] is not JObject contentPackForObject)
        {
            return null;
        }

        return new ContentPackReference
        {
            UniqueID = this.ReadString(contentPackForObject, "UniqueID"),
            MinimumVersion = this.ReadOptionalString(contentPackForObject, "MinimumVersion")
        };
    }

    private string ReadString(JObject element, string propertyName)
    {
        return this.ReadOptionalString(element, propertyName) ?? string.Empty;
    }

    private string? ReadOptionalString(JObject element, string propertyName)
    {
        if (element[propertyName] is not JToken token)
        {
            return null;
        }

        return token.Type == JTokenType.String
            ? token.Value<string>()
            : token.ToString(Newtonsoft.Json.Formatting.None);
    }
}
