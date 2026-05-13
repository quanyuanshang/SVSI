namespace StardewStoryInspector.Models;

public sealed class EventKeySplitResult
{
    public string EventId { get; init; } = string.Empty;

    public List<string> PreconditionFragments { get; init; } = new();

    public List<string> Warnings { get; init; } = new();
}
