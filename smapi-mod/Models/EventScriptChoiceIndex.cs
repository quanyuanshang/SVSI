namespace StardewStoryInspector.Models;

public sealed class EventScriptChoiceIndex
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int EntryCount { get; init; }

    public List<EventScriptChoiceEntry> Entries { get; init; } = new();
}
