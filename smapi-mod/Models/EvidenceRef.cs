namespace StardewStoryInspector.Models;

public sealed class EvidenceRef
{
    public string Kind { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public string? JsonPath { get; init; }
}
