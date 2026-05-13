namespace StardewStoryInspector.Models;

public sealed class ModScanReport
{
    public string ModsDirectory { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public List<ScannedMod> Mods { get; init; } = new();

    public string? ScanError { get; init; }
}
