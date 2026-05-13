namespace StardewStoryInspector.Models;

public sealed class ContentPackReference
{
    public string UniqueID { get; init; } = string.Empty;

    public string? MinimumVersion { get; init; }
}
