using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class RuntimeStoryStateRefreshService
{
    private readonly Func<RuntimeGameState?> collectState;
    private readonly StoryStateEvaluationExporter storyStateEvaluationExporter;
    private readonly StoryStateEvaluator storyStateEvaluator;
    private readonly EventHistoryTracker eventHistoryTracker;
    private readonly TranslationCatalog translationCatalog;
    private readonly Action<string>? logInfo;
    private readonly Action<string>? logWarning;
    private readonly Action<string>? logDebug;
    private readonly Action<string>? logError;

    public RuntimeStoryStateRefreshService(
        Func<RuntimeGameState?> collectState,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null,
        Action<string>? logDebug = null,
        Action<string>? logError = null)
        : this(
            collectState,
            new StoryStateEvaluationExporter(),
            new StoryStateEvaluator(),
            new EventHistoryTracker(),
            new TranslationCatalog(),
            logInfo,
            logWarning,
            logDebug,
            logError)
    {
    }

    public RuntimeStoryStateRefreshService(
        Func<RuntimeGameState?> collectState,
        StoryStateEvaluationExporter storyStateEvaluationExporter,
        StoryStateEvaluator storyStateEvaluator,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null,
        Action<string>? logDebug = null,
        Action<string>? logError = null)
        : this(
            collectState,
            storyStateEvaluationExporter,
            storyStateEvaluator,
            new EventHistoryTracker(),
            new TranslationCatalog(),
            logInfo,
            logWarning,
            logDebug,
            logError)
    {
    }

    public RuntimeStoryStateRefreshService(
        Func<RuntimeGameState?> collectState,
        StoryStateEvaluationExporter storyStateEvaluationExporter,
        StoryStateEvaluator storyStateEvaluator,
        EventHistoryTracker eventHistoryTracker,
        TranslationCatalog translationCatalog,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null,
        Action<string>? logDebug = null,
        Action<string>? logError = null)
    {
        this.collectState = collectState;
        this.storyStateEvaluationExporter = storyStateEvaluationExporter;
        this.storyStateEvaluator = storyStateEvaluator;
        this.eventHistoryTracker = eventHistoryTracker;
        this.translationCatalog = translationCatalog;
        this.logInfo = logInfo;
        this.logWarning = logWarning;
        this.logDebug = logDebug;
        this.logError = logError;
    }

    public StoryStateEvaluationReport? Refresh(
        string storyIndexPath,
        string evaluatedOutputPath,
        string exportStatePath)
    {
        if (!File.Exists(storyIndexPath))
        {
            this.logWarning?.Invoke($"Story index file was not found: {storyIndexPath}");
            return null;
        }

        var runtimeState = this.collectState();
        if (runtimeState is null)
        {
            this.logDebug?.Invoke("Runtime state refresh skipped because the world is not ready.");
            return null;
        }

        try
        {
            var index = this.storyStateEvaluationExporter.LoadStoryRawEventIndex(storyIndexPath);
            var report = this.storyStateEvaluator.Evaluate(index.Nodes, runtimeState, this.translationCatalog, index.ModConfigByUniqueId);

            EnsureParentDirectory(evaluatedOutputPath);
            EnsureParentDirectory(exportStatePath);

            File.WriteAllText(
                evaluatedOutputPath,
                System.Text.Json.JsonSerializer.Serialize(report, JsonExportOptions.Default)
            );
            File.WriteAllText(
                exportStatePath,
                System.Text.Json.JsonSerializer.Serialize(ToExportState(runtimeState), JsonExportOptions.Default)
            );

            this.TryWriteEventHistory(evaluatedOutputPath, runtimeState, index.Nodes);

            this.logInfo?.Invoke(
                $"Story state refreshed: Current={GetStatusCount(report, StoryNodeStatus.Current)}, " +
                $"AvailableLater={GetStatusCount(report, StoryNodeStatus.AvailableLater)}, " +
                $"Locked={GetStatusCount(report, StoryNodeStatus.Locked)}, " +
                $"Triggered={GetStatusCount(report, StoryNodeStatus.Triggered)}, " +
                $"Unknown={GetStatusCount(report, StoryNodeStatus.Unknown)}"
            );

            return report;
        }
        catch (Exception ex)
        {
            this.logError?.Invoke($"Failed to refresh story state: {ex.Message}");
            return null;
        }
    }

    private void TryWriteEventHistory(
        string evaluatedOutputPath,
        RuntimeGameState runtimeState,
        IReadOnlyCollection<StoryNode> storyNodes)
    {
        try
        {
            var historyPath = GetEventHistoryPath(evaluatedOutputPath);
            var report = EventHistoryStore.LoadOrCreate(
                historyPath,
                new SaveIdentity
                {
                    FarmerName = runtimeState.PlayerName
                }
            );

            this.eventHistoryTracker.Track(report, runtimeState, storyNodes);
            EventHistoryStore.Save(historyPath, report);
        }
        catch (Exception ex)
        {
            this.logWarning?.Invoke($"Failed to update event history: {ex.Message}");
        }
    }

    private static string GetEventHistoryPath(string evaluatedOutputPath)
    {
        var stateDirectory = Path.GetDirectoryName(evaluatedOutputPath) ?? string.Empty;
        var runtimeDirectory = string.Equals(
            Path.GetFileName(stateDirectory),
            "state",
            StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(stateDirectory)?.FullName ?? stateDirectory
            : stateDirectory;
        return Path.Combine(runtimeDirectory, "history", "event-history.json");
    }

    private static ExportState ToExportState(RuntimeGameState state)
    {
        return new ExportState
        {
            Year = state.Year,
            Season = state.Season,
            Day = state.DayOfMonth,
            Time = state.Time,
            Weather = state.Weather,
            PlayerName = state.PlayerName
        };
    }

    private static int GetStatusCount(StoryStateEvaluationReport report, StoryNodeStatus status)
    {
        return report.StatusCounts.TryGetValue(status.ToString(), out var count)
            ? count
            : 0;
    }

    private static void EnsureParentDirectory(string outputPath)
    {
        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
