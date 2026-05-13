using System.Text.Json;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventHistoryStoreTests
{
    public static void RunAll()
    {
        LoadOrCreate_MissingFile_ReturnsNewReportForIdentity();
        AddIfMissing_PreventsDuplicateEventId();
        Save_WritesSortedIndentedCamelCaseJson();
        LoadOrCreate_BrokenJson_BacksUpFileAndReturnsNewReport();
    }

    private static void LoadOrCreate_MissingFile_ReturnsNewReportForIdentity()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "event-history.json");
        var identity = new SaveIdentity
        {
            FarmerName = "MockFarmer",
            FarmName = "MockFarm",
            SaveId = "save-1"
        };

        var report = EventHistoryStore.LoadOrCreate(path, identity);

        AssertEqual("MockFarmer", report.Identity.FarmerName, "Report should keep the provided farmer name.");
        AssertEqual("MockFarm", report.Identity.FarmName, "Report should keep the provided farm name.");
        AssertEqual("save-1", report.Identity.SaveId, "Report should keep the provided save id.");
        AssertEqual(0, report.Entries.Count, "New event history reports should start empty.");
    }

    private static void AddIfMissing_PreventsDuplicateEventId()
    {
        var report = new EventHistoryReport();
        var first = Entry("100", "spring", 4, 900);
        var duplicate = Entry("100", "winter", 20, 1800);

        var addedFirst = EventHistoryStore.AddIfMissing(report, first);
        var addedDuplicate = EventHistoryStore.AddIfMissing(report, duplicate);

        AssertTrue(addedFirst, "First event history entry should be added.");
        AssertTrue(!addedDuplicate, "Duplicate event ids should not be added.");
        AssertEqual(1, report.Entries.Count, "Report should contain only one entry for a duplicate event id.");
        AssertEqual("spring", report.Entries[0].Date.Season, "Duplicate entry should not replace the existing entry.");
    }

    private static void Save_WritesSortedIndentedCamelCaseJson()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "event-history.json");
        var report = new EventHistoryReport
        {
            Identity = new SaveIdentity { FarmerName = "MockFarmer" },
            Entries =
            {
                Entry("winter-event", "winter", 1, 600),
                Entry("spring-late", "spring", 3, 1200),
                Entry("spring-early", "spring", 3, 900),
                Entry("summer-event", "summer", 1, 600),
                Entry("fall-event", "fall", 1, 600)
            }
        };

        EventHistoryStore.Save(path, report);

        var rawJson = File.ReadAllText(path);
        using var document = JsonDocument.Parse(rawJson);
        var entries = document.RootElement.GetProperty("entries");

        AssertTrue(rawJson.Contains(Environment.NewLine, StringComparison.Ordinal), "Saved JSON should be indented.");
        AssertTrue(document.RootElement.TryGetProperty("generatedAtUtc", out _), "Saved JSON should use camelCase properties.");
        AssertEqual("spring-early", entries[0].GetProperty("eventId").GetString(), "Spring entry at 900 should sort first.");
        AssertEqual("spring-late", entries[1].GetProperty("eventId").GetString(), "Spring entry at 1200 should sort second.");
        AssertEqual("summer-event", entries[2].GetProperty("eventId").GetString(), "Summer should sort after spring.");
        AssertEqual("fall-event", entries[3].GetProperty("eventId").GetString(), "Fall should sort after summer.");
        AssertEqual("winter-event", entries[4].GetProperty("eventId").GetString(), "Winter should sort after fall.");
    }

    private static void LoadOrCreate_BrokenJson_BacksUpFileAndReturnsNewReport()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "event-history.json");
        File.WriteAllText(path, "{ broken json");

        var report = EventHistoryStore.LoadOrCreate(
            path,
            new SaveIdentity { FarmerName = "BackupFarmer" }
        );

        var backupFiles = Directory.GetFiles(directory, "event-history.broken.*.json");
        AssertEqual(1, backupFiles.Length, "Broken event history JSON should be backed up once.");
        AssertEqual("{ broken json", File.ReadAllText(backupFiles[0]), "Backup should preserve the broken JSON.");
        AssertEqual("BackupFarmer", report.Identity.FarmerName, "Broken JSON should return a new report for the provided identity.");
        AssertEqual(0, report.Entries.Count, "Replacement report should start empty.");
    }

    private static ObservedEventHistoryEntry Entry(string eventId, string season, int dayOfMonth, int time)
    {
        return new ObservedEventHistoryEntry
        {
            EventId = eventId,
            Date = new GameDateSnapshot
            {
                Year = 1,
                Season = season,
                DayOfMonth = dayOfMonth,
                Time = time
            },
            Location = "Town"
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(EventHistoryStoreTests),
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(directory);
        return directory;
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
