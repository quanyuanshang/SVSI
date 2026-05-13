namespace StardewStoryInspector.Models;

public sealed class DialogueIndex
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int EntryCount { get; init; }

    public List<DialogueIndexEntry> Entries { get; init; } = new();
}
