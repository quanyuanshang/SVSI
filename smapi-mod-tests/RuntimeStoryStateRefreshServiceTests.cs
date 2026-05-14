using System.Text.Json;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class RuntimeStoryStateRefreshServiceTests
{
    public static void RunAll()
    {
        Refresh_WritesEvaluatedAndExportStateFiles();
        Refresh_MissingStoryIndex_LogsWarningAndReturnsNull();
    }

    private static void Refresh_WritesEvaluatedAndExportStateFiles()
    {
        var infoLogs = new List<string>();
        var warningLogs = new List<string>();
        var errorLogs = new List<string>();
        var service = new RuntimeStoryStateRefreshService(
            CreateRuntimeState,
            logInfo: message => infoLogs.Add(message),
            logWarning: message => warningLogs.Add(message),
            logError: message => errorLogs.Add(message)
        );

        var baseDirectory = AppContext.BaseDirectory;
        var storyIndexPath = Path.Combine(baseDirectory, "TestData", "story-index.linked.mock.json");
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(RuntimeStoryStateRefreshServiceTests),
            "RuntimeRefresh",
            Guid.NewGuid().ToString("N")
        );
        var evaluatedOutputPath = Path.Combine(outputDirectory, "story-state.evaluated.json");
        var exportStatePath = Path.Combine(outputDirectory, "state.json");

        if (File.Exists(evaluatedOutputPath))
        {
            File.Delete(evaluatedOutputPath);
        }

        if (File.Exists(exportStatePath))
        {
            File.Delete(exportStatePath);
        }

        var report = service.Refresh(storyIndexPath, evaluatedOutputPath, exportStatePath);

        AssertTrue(report is not null, "Refresh should return an evaluation report.");
        AssertTrue(File.Exists(evaluatedOutputPath), "Refresh should write story-state.evaluated.json.");
        AssertTrue(File.Exists(exportStatePath), "Refresh should write export/state.json.");
        AssertEqual(0, warningLogs.Count, "Refresh should not emit warnings for valid input.");
        AssertEqual(0, errorLogs.Count, "Refresh should not emit errors for valid input.");
        AssertTrue(infoLogs.Any(log => log.Contains("Story state refreshed:", StringComparison.Ordinal)), "Refresh should log summary counts.");

        using var evaluatedDocument = JsonDocument.Parse(File.ReadAllText(evaluatedOutputPath));
        AssertTrue(evaluatedDocument.RootElement.TryGetProperty("statusCounts", out _), "Evaluated output should include statusCounts.");
        AssertTrue(evaluatedDocument.RootElement.TryGetProperty("nodes", out _), "Evaluated output should include nodes.");
        AssertEqual("Hovsep", evaluatedDocument.RootElement.GetProperty("runtimeState").GetProperty("spouseName").GetString(), "Evaluated output should preserve spouseName.");
        AssertEqual("Hovsep", evaluatedDocument.RootElement.GetProperty("runtimeState").GetProperty("marriedTo").GetString(), "Evaluated output should preserve marriedTo.");
        AssertEqual("Sebastian", evaluatedDocument.RootElement.GetProperty("runtimeState").GetProperty("engagedTo").GetString(), "Evaluated output should preserve engagedTo.");
        AssertEqual("Krobus", evaluatedDocument.RootElement.GetProperty("runtimeState").GetProperty("roommate").GetString(), "Evaluated output should preserve roommate.");
        AssertTrue(
            evaluatedDocument.RootElement.GetProperty("runtimeState").GetProperty("installedModIds").EnumerateArray().Any(item => item.GetString() == "Pathoschild.ContentPatcher"),
            "Evaluated output should preserve installedModIds."
        );

        using var exportStateDocument = JsonDocument.Parse(File.ReadAllText(exportStatePath));
        AssertEqual("fall", exportStateDocument.RootElement.GetProperty("season").GetString(), "Export state should preserve season.");
        AssertEqual("MockFarmer", exportStateDocument.RootElement.GetProperty("playerName").GetString(), "Export state should preserve player name.");
    }

    private static void Refresh_MissingStoryIndex_LogsWarningAndReturnsNull()
    {
        var warningLogs = new List<string>();
        var service = new RuntimeStoryStateRefreshService(
            CreateRuntimeState,
            logWarning: message => warningLogs.Add(message)
        );

        var baseDirectory = AppContext.BaseDirectory;
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(RuntimeStoryStateRefreshServiceTests),
            "RuntimeRefreshMissing",
            Guid.NewGuid().ToString("N")
        );
        var evaluatedOutputPath = Path.Combine(outputDirectory, "story-state.evaluated.json");
        var exportStatePath = Path.Combine(outputDirectory, "state.json");
        var missingStoryIndexPath = Path.Combine(baseDirectory, "TestData", "missing-story-index.json");

        var report = service.Refresh(missingStoryIndexPath, evaluatedOutputPath, exportStatePath);

        AssertTrue(report is null, "Refresh should return null when the story index is missing.");
        AssertTrue(warningLogs.Any(log => log.Contains("Story index file was not found", StringComparison.Ordinal)), "Refresh should log a warning for missing story index.");
    }

    private static RuntimeGameState CreateRuntimeState()
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            DayOfWeek = "Friday",
            Time = 1900,
            Weather = "sunny",
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            InstalledModIds = new HashSet<string>(new[] { "Pathoschild.ContentPatcher" }, StringComparer.Ordinal),
            FriendshipPoints = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Shane"] = 2200,
                ["Sam"] = 1000
            },
            SpouseName = "Hovsep",
            Spouse = "Hovsep",
            MarriedTo = "Hovsep",
            Spouses = new[] { "Hovsep" },
            EngagedTo = "Sebastian",
            Roommate = "Krobus",
            DatingNpcNames = new HashSet<string>(new[] { "Sam" }, StringComparer.Ordinal),
            SeenEvents = new HashSet<string>(StringComparer.Ordinal),
            Mail = new HashSet<string>(new[] { "someMail" }, StringComparer.Ordinal),
            DialogueAnswers = new HashSet<string>(new[] { "ShaneAnswerA" }, StringComparer.Ordinal)
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
