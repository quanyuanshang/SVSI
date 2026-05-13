using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventHistoryTrackerTests
{
    public static void RunAll()
    {
        Track_FirstCall_ImportsExistingSeenEvents();
        Track_FirstCall_SkipsSeenEventsWithoutStoryNodeMatch();
        Track_LaterCall_AddsOnlyDeltaEvents();
        Track_DoesNotAddDuplicates();
        Track_MatchesStoryNodeDetails();
    }

    private static void Track_FirstCall_ImportsExistingSeenEvents()
    {
        var tracker = new EventHistoryTracker();
        var report = new EventHistoryReport();
        var state = State("100", "200");

        var added = tracker.Track(report, state, new[] { Node("100"), Node("200") });

        AssertEqual(2, added, "First tracking call should import all current seen events.");
        AssertEqual(2, report.Entries.Count, "Report should contain imported seen events.");
        AssertTrue(report.Entries.All(entry => entry.ObservationSource == "eventsSeen-existing"), "First import entries should be marked as existing seen events.");
        AssertEqual(1, report.Entries[0].FirstSeenGameDate.Year, "Entry date should use runtime year.");
        AssertEqual("fall", report.Entries[0].FirstSeenGameDate.Season, "Entry date should use runtime season.");
        AssertEqual(12, report.Entries[0].FirstSeenGameDate.DayOfMonth, "Entry date should use runtime day.");
        AssertEqual(1900, report.Entries[0].FirstSeenGameDate.Time, "Entry date should use runtime time.");
    }

    private static void Track_FirstCall_SkipsSeenEventsWithoutStoryNodeMatch()
    {
        var tracker = new EventHistoryTracker();
        var report = new EventHistoryReport();

        var added = tracker.Track(report, State("known", "vanillaUnknown"), new[] { Node("known") });

        AssertEqual(1, added, "Only story-index events should be imported into history.");
        AssertEqual("known", report.Entries.Single().EventId, "Unmatched seen events should be skipped.");
    }

    private static void Track_LaterCall_AddsOnlyDeltaEvents()
    {
        var tracker = new EventHistoryTracker();
        var report = new EventHistoryReport();

        var nodes = new[] { Node("100"), Node("200") };
        tracker.Track(report, State("100"), nodes);
        var added = tracker.Track(report, State("100", "200"), nodes);

        AssertEqual(1, added, "Second tracking call should add only newly seen events.");
        AssertEqual(2, report.Entries.Count, "Report should contain initial and delta entries.");
        AssertEqual("eventsSeen-delta", report.Entries.Single(entry => entry.EventId == "200").ObservationSource, "Delta entry should be marked as newly seen.");
    }

    private static void Track_DoesNotAddDuplicates()
    {
        var tracker = new EventHistoryTracker();
        var report = new EventHistoryReport
        {
            Entries =
            {
                new ObservedEventHistoryEntry
                {
                    EventId = "100",
                    Date = Date(),
                    FirstSeenGameDate = Date()
                }
            }
        };

        var added = tracker.Track(report, State("100"), new[] { Node("100") });

        AssertEqual(0, added, "Tracking should not add an event already in history.");
        AssertEqual(1, report.Entries.Count, "Tracking should preserve a single history entry per event id.");
    }

    private static void Track_MatchesStoryNodeDetails()
    {
        var tracker = new EventHistoryTracker();
        var report = new EventHistoryReport();
        var node = new StoryNode
        {
            NodeId = "node-100",
            EventId = "100",
            SourceModId = "author.mod",
            SourceModName = "Author Mod",
            Location = "Forest"
        };

        tracker.Track(report, State("100"), new[] { node });

        var entry = report.Entries.Single();
        AssertEqual("node-100", entry.NodeId, "Tracker should copy matching node id.");
        AssertEqual("author.mod", entry.SourceModId, "Tracker should copy matching source mod id.");
        AssertEqual("Author Mod", entry.SourceModName, "Tracker should copy matching source mod name.");
        AssertEqual("Forest", entry.Location, "Tracker should prefer matching node location.");
    }

    private static RuntimeGameState State(params string[] seenEvents)
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            Time = 1900,
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            SeenEvents = new HashSet<string>(seenEvents, StringComparer.Ordinal)
        };
    }

    private static StoryNode Node(string eventId)
    {
        return new StoryNode
        {
            NodeId = $"node-{eventId}",
            EventId = eventId,
            SourceModId = "author.mod",
            SourceModName = "Author Mod",
            Location = "Town"
        };
    }

    private static GameDateSnapshot Date()
    {
        return new GameDateSnapshot
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            Time = 1900
        };
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
