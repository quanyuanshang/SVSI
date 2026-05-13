using System.Text.Json.Nodes;

namespace StardewStoryInspector.Models;

public sealed class ScannedMod
{
    public string DirectoryPath { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string UniqueID { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public ContentPackReference? ContentPackFor { get; init; }

    public bool IsContentPatcherContentPack { get; init; }

    public string? ContentJsonPath { get; init; }

    public JsonNode? ContentJson { get; init; }

    public string? ContentJsonReadMode { get; init; }

    public long? ContentJsonSize { get; init; }

    public string? ContentJsonSha256 { get; init; }

    public string? ContentJsonError { get; init; }

    public string? ScanError { get; init; }
}
