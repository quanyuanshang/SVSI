using System.Text.Json;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class RuntimeStoryStateRefreshServiceHistoryTests
{
    public static void RunAll()
    {
        Refresh_WritesEventHistoryFile();
        Refresh_WritesEventHistoryUnderRuntimeDirectoryWhenExportStateIsOutsideRuntime();
    }

    private static void Refresh_WritesEventHistoryFile()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(RuntimeStoryStateRefreshServiceHistoryTests),
            Guid.NewGuid().ToString("N")
        );
        var storyIndexPath = Path.Combine(outputDirectory, "story-index.json");
        var evaluatedOutputPath = Path.Combine(outputDirectory, "runtime", "story-state.evaluated.json");
        var exportStatePath = Path.Combine(outputDirectory, "runtime", "state.json");
        var historyPath = Path.Combine(outputDirectory, "runtime", "history", "event-history.json");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            storyIndexPath,
            JsonSerializer.Serialize(
                new
                {
                    nodes = new[]
                    {
                        new
                        {
                            nodeId = "node-100",
                            eventId = "100",
                            sourceModId = "author.mod",
                            sourceModName = "Author Mod",
                            location = "Forest"
                        }
                    }
                },
                JsonExportOptions.Default
            )
        );

        var service = new RuntimeStoryStateRefreshService(CreateRuntimeState);

        var report = service.Refresh(storyIndexPath, evaluatedOutputPath, exportStatePath);

        AssertTrue(report is not null, "Refresh should still return the story state report.");
        AssertTrue(File.Exists(evaluatedOutputPath), "Refresh should write story-state.evaluated.json.");
        AssertTrue(File.Exists(historyPath), "Refresh should write runtime/history/event-history.json.");

        using var historyDocument = JsonDocument.Parse(File.ReadAllText(historyPath));
        var entry = historyDocument.RootElement.GetProperty("entries")[0];
        AssertEqual("100", entry.GetProperty("eventId").GetString(), "History should contain the seen event id.");
        AssertEqual("node-100", entry.GetProperty("nodeId").GetString(), "History should include matched story node id.");
        AssertEqual("eventsSeen-existing", entry.GetProperty("observationSource").GetString(), "First refresh should import current seen events.");
    }

    private static void Refresh_WritesEventHistoryUnderRuntimeDirectoryWhenExportStateIsOutsideRuntime()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(RuntimeStoryStateRefreshServiceHistoryTests),
            Guid.NewGuid().ToString("N")
        );
        var storyIndexPath = Path.Combine(outputDirectory, "runtime", "index", "story-index.json");
        var evaluatedOutputPath = Path.Combine(outputDirectory, "runtime", "state", "story-state.evaluated.json");
        var exportStatePath = Path.Combine(outputDirectory, "export", "state.json");
        var expectedHistoryPath = Path.Combine(outputDirectory, "runtime", "history", "event-history.json");
        var wrongHistoryPath = Path.Combine(outputDirectory, "export", "history", "event-history.json");

        Directory.CreateDirectory(Path.GetDirectoryName(storyIndexPath)!);
        File.WriteAllText(
            storyIndexPath,
            JsonSerializer.Serialize(
                new
                {
                    nodes = new[]
                    {
                        new
                        {
                            nodeId = "node-100",
                            eventId = "100",
                            sourceModId = "author.mod",
                            sourceModName = "Author Mod",
                            location = "Forest"
                        }
                    }
                },
                JsonExportOptions.Default
            )
        );

        var service = new RuntimeStoryStateRefreshService(CreateRuntimeState);

        var report = service.Refresh(storyIndexPath, evaluatedOutputPath, exportStatePath);

        AssertTrue(report is not null, "Refresh should return the story state report.");
        AssertTrue(File.Exists(expectedHistoryPath), "History should be written under runtime/history.");
        AssertTrue(!File.Exists(wrongHistoryPath), "History should not be written under export/history.");
    }

    private static RuntimeGameState CreateRuntimeState()
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            Time = 1900,
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            SeenEvents = new HashSet<string>(new[] { "100" }, StringComparer.Ordinal)
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
