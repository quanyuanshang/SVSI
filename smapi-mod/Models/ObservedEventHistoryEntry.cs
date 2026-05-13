namespace StardewStoryInspector.Models;

public sealed class ObservedEventHistoryEntry
{
    public string EventId { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public string SourceModId { get; init; } = string.Empty;

    public string SourceModName { get; init; } = string.Empty;

    public string ObservationSource { get; init; } = string.Empty;

    public GameDateSnapshot FirstSeenGameDate { get; init; } = new();

    public GameDateSnapshot Date { get; init; } = new();

    public string Location { get; init; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
