namespace StardewStoryInspector.Models;

public sealed class StoryRawEventIndex
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int NodeCount { get; init; }

    public List<StoryNode> Nodes { get; init; } = new();
}
