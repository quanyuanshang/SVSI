using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector;

public sealed class ModEntry : Mod
{
    private RuntimeStoryStateRefreshService? refreshService;
    private string exportDirectory = string.Empty;
    private string exportStatePath = string.Empty;
    private string runtimeIndexDirectory = string.Empty;
    private string runtimeStateDirectory = string.Empty;
    private string linkedStoryIndexPath = string.Empty;
    private string evaluatedStoryStatePath = string.Empty;
    private int? lastTimeOfDay;

    public override void Entry(IModHelper helper)
    {
        this.Monitor.Log("StardewStoryInspector loaded", LogLevel.Info);

        var gameDirectory = AppContext.BaseDirectory;
        this.exportDirectory = Path.GetFullPath(Path.Combine(gameDirectory, "StardewStoryInspector", "export"));
        this.runtimeIndexDirectory = Path.GetFullPath(
            Path.Combine(gameDirectory, "StardewStoryInspector", "runtime", "index")
        );
        this.runtimeStateDirectory = Path.GetFullPath(
            Path.Combine(gameDirectory, "StardewStoryInspector", "runtime", "state")
        );
        var modsDirectory = Path.Combine(gameDirectory, "Mods");
        this.exportStatePath = Path.Combine(this.exportDirectory, "state.json");
        this.linkedStoryIndexPath = Path.Combine(this.runtimeIndexDirectory, "story-index.linked.json");
        this.evaluatedStoryStatePath = Path.Combine(this.runtimeStateDirectory, "story-state.evaluated.json");

        Directory.CreateDirectory(this.exportDirectory);
        Directory.CreateDirectory(this.runtimeIndexDirectory);
        Directory.CreateDirectory(this.runtimeStateDirectory);

        this.WriteJson(
            this.exportStatePath,
            new ExportState
            {
                Year = 1,
                Season = "spring",
                Day = 1,
                Time = 600,
                Weather = "sunny",
                PlayerName = "MockFarmer"
            }
        );

        var modScanReport = new ModScanner(this.Monitor).Scan(modsDirectory);
        this.WriteJson(Path.Combine(this.exportDirectory, "mods.json"), modScanReport);

        var storyIndex = new EventIndexBuilder().Build(modScanReport.Mods);
        this.WriteJson(
            Path.Combine(this.runtimeIndexDirectory, "story-index.raw-events.json"),
            storyIndex
        );
        var dialogueIndex = new DialogueIndexParser().Build(modScanReport.Mods);
        this.WriteJson(
            Path.Combine(this.runtimeIndexDirectory, "dialogue-index.json"),
            dialogueIndex
        );
        var eventScriptChoiceIndex = new EventScriptChoiceIndexParser().Build(modScanReport.Mods);
        this.WriteJson(
            Path.Combine(this.runtimeIndexDirectory, "event-script-choice-index.json"),
            eventScriptChoiceIndex
        );
        var storyIndexPath = Path.Combine(this.runtimeIndexDirectory, "story-index.raw-events.json");
        var dialogueIndexPath = Path.Combine(this.runtimeIndexDirectory, "dialogue-index.json");
        var eventScriptChoiceIndexPath = Path.Combine(this.runtimeIndexDirectory, "event-script-choice-index.json");
        var linkedStoryIndex = new StoryNodeDialogueLinker().Link(
            storyIndexPath,
            dialogueIndexPath,
            eventScriptChoiceIndexPath
        );
        this.WriteJson(this.linkedStoryIndexPath, linkedStoryIndex);

        this.Monitor.Log(
            $"Scanned {modScanReport.Mods.Count} mod manifests into mods.json",
            LogLevel.Info
        );
        this.Monitor.Log(
            $"Indexed {storyIndex.NodeCount} raw event nodes into story-index.raw-events.json",
            LogLevel.Info
        );
        this.Monitor.Log(
            $"Indexed {dialogueIndex.EntryCount} dialogue entries into dialogue-index.json",
            LogLevel.Info
        );
        this.Monitor.Log(
            $"Indexed {eventScriptChoiceIndex.EntryCount} event script choice entries into event-script-choice-index.json",
            LogLevel.Info
        );
        this.Monitor.Log(
            $"Linked {linkedStoryIndex.NodeCount} story nodes into story-index.linked.json",
            LogLevel.Info
        );

        var runtimeStateCollector = new RuntimeStateCollector();
        this.refreshService = new RuntimeStoryStateRefreshService(
            runtimeStateCollector.Collect,
            logInfo: message => this.Monitor.Log(message, LogLevel.Info),
            logWarning: message => this.Monitor.Log(message, LogLevel.Warn),
            logDebug: message => this.Monitor.Log(message, LogLevel.Debug),
            logError: message => this.Monitor.Log(message, LogLevel.Error)
        );

        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
    }

    private void WriteJson<T>(string outputPath, T payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonExportOptions.Default);
        File.WriteAllText(outputPath, json);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.lastTimeOfDay = Game1.timeOfDay;
        this.RefreshStoryState();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsLocalPlayer)
        {
            return;
        }

        this.RefreshStoryState();
    }

    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        var currentTimeOfDay = Game1.timeOfDay;
        if (this.lastTimeOfDay == currentTimeOfDay)
        {
            return;
        }

        this.lastTimeOfDay = currentTimeOfDay;
        this.RefreshStoryState();
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        var oldMenuName = e.OldMenu?.GetType().Name ?? "null";
        var newMenuName = e.NewMenu?.GetType().Name ?? "null";
        this.Monitor.Log($"Menu changed: {oldMenuName} -> {newMenuName}", LogLevel.Debug);
    }

    private void RefreshStoryState()
    {
        this.refreshService?.Refresh(
            this.linkedStoryIndexPath,
            this.evaluatedStoryStatePath,
            this.exportStatePath
        );
    }
}
