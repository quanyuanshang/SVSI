using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class EventHistoryTracker
{
    private readonly HashSet<string> _lastSeenEvents = new(StringComparer.Ordinal);
    private bool hasTracked;

    public int Track(
        EventHistoryReport report,
        RuntimeGameState currentState,
        IEnumerable<StoryNode> storyNodes)
    {
        var nodesByEventId = storyNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.EventId))
            .GroupBy(node => node.EventId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (!this.hasTracked)
        {
            foreach (var entry in report.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.EventId))
                {
                    this._lastSeenEvents.Add(entry.EventId);
                }
            }
        }

        var observationSource = this.hasTracked
            ? "eventsSeen-delta"
            : "eventsSeen-existing";
        var addedCount = 0;

        foreach (var eventId in currentState.SeenEvents.OrderBy(eventId => eventId, StringComparer.Ordinal))
        {
            if (this._lastSeenEvents.Contains(eventId))
            {
                continue;
            }

            if (!nodesByEventId.ContainsKey(eventId))
            {
                this._lastSeenEvents.Add(eventId);
                continue;
            }

            if (EventHistoryStore.AddIfMissing(report, CreateEntry(eventId, observationSource, currentState, nodesByEventId)))
            {
                addedCount++;
            }

            this._lastSeenEvents.Add(eventId);
        }

        this.hasTracked = true;
        return addedCount;
    }

    private static ObservedEventHistoryEntry CreateEntry(
        string eventId,
        string observationSource,
        RuntimeGameState currentState,
        IReadOnlyDictionary<string, StoryNode> nodesByEventId)
    {
        nodesByEventId.TryGetValue(eventId, out var node);
        var date = new GameDateSnapshot
        {
            Year = currentState.Year,
            Season = currentState.Season,
            DayOfMonth = currentState.DayOfMonth,
            Time = currentState.Time
        };

        return new ObservedEventHistoryEntry
        {
            EventId = eventId,
            NodeId = node?.NodeId ?? string.Empty,
            SourceModId = node?.SourceModId ?? string.Empty,
            SourceModName = node?.SourceModName ?? string.Empty,
            ObservationSource = observationSource,
            FirstSeenGameDate = date,
            Date = date,
            Location = string.IsNullOrWhiteSpace(node?.Location)
                ? currentState.CurrentLocation
                : node.Location
        };
    }
}
