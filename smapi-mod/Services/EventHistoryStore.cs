using System.Text.Json;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public static class EventHistoryStore
{
    public static EventHistoryReport LoadOrCreate(string path, SaveIdentity identity)
    {
        EnsurePath(path);

        if (!File.Exists(path))
        {
            return CreateReport(identity);
        }

        try
        {
            var report = JsonSerializer.Deserialize<EventHistoryReport>(
                File.ReadAllText(path),
                JsonExportOptions.Default
            );

            if (report is null)
            {
                return CreateReport(identity);
            }

            SortEntries(report);
            return report;
        }
        catch (JsonException)
        {
            BackUpBrokenJson(path);
            return CreateReport(identity);
        }
    }

    public static bool AddIfMissing(EventHistoryReport report, ObservedEventHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EventId))
        {
            throw new ArgumentException("Event history entries require an event id.", nameof(entry));
        }

        if (report.Entries.Any(existing => string.Equals(existing.EventId, entry.EventId, StringComparison.Ordinal)))
        {
            return false;
        }

        report.Entries.Add(entry);
        return true;
    }

    public static void Save(string path, EventHistoryReport report)
    {
        EnsurePath(path);
        SortEntries(report);
        report.GeneratedAtUtc = DateTimeOffset.UtcNow;

        var outputDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonExportOptions.Default));
    }

    public static void SortEntries(EventHistoryReport report)
    {
        report.Entries.Sort(CompareEntries);
    }

    private static EventHistoryReport CreateReport(SaveIdentity identity)
    {
        return new EventHistoryReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Identity = identity
        };
    }

    private static int CompareEntries(ObservedEventHistoryEntry left, ObservedEventHistoryEntry right)
    {
        var leftDate = left.Date;
        var rightDate = right.Date;

        var yearComparison = leftDate.Year.CompareTo(rightDate.Year);
        if (yearComparison != 0)
        {
            return yearComparison;
        }

        var seasonComparison = SeasonOrder(leftDate.Season).CompareTo(SeasonOrder(rightDate.Season));
        if (seasonComparison != 0)
        {
            return seasonComparison;
        }

        var dayComparison = leftDate.DayOfMonth.CompareTo(rightDate.DayOfMonth);
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        var timeComparison = leftDate.Time.CompareTo(rightDate.Time);
        if (timeComparison != 0)
        {
            return timeComparison;
        }

        return string.Compare(left.EventId, right.EventId, StringComparison.Ordinal);
    }

    private static int SeasonOrder(string season)
    {
        return season.ToLowerInvariant() switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => int.MaxValue
        };
    }

    private static void BackUpBrokenJson(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var backupPath = Path.Combine(directory, $"{fileName}.broken.{timestamp}{extension}");

        for (var attempt = 1; File.Exists(backupPath); attempt++)
        {
            backupPath = Path.Combine(directory, $"{fileName}.broken.{timestamp}.{attempt}{extension}");
        }

        File.Move(path, backupPath);
    }

    private static void EnsurePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Event history path is required.", nameof(path));
        }
    }
}
