namespace StardewStoryInspector.Models;

public sealed class EventHistoryReport
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SaveIdentity Identity { get; set; } = new();

    public List<ObservedEventHistoryEntry> Entries { get; set; } = new();
}
