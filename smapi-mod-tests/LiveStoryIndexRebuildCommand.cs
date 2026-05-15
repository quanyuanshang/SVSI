using System.Text.Json;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class LiveStoryIndexRebuildCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: rebuild-live-story-index <StoryInspectorRoot>");
            return 1;
        }

        var rootDirectory = args[1];
        var exportDirectory = Path.Combine(rootDirectory, "export");
        var runtimeIndexDirectory = Path.Combine(rootDirectory, "runtime", "index");
        var runtimeStateDirectory = Path.Combine(rootDirectory, "runtime", "state");

        var modsPath = Path.Combine(exportDirectory, "mods.json");
        var dialogueIndexPath = Path.Combine(runtimeIndexDirectory, "dialogue-index.json");
        var eventChoiceIndexPath = Path.Combine(runtimeIndexDirectory, "event-script-choice-index.json");
        var rawStoryIndexPath = Path.Combine(runtimeIndexDirectory, "story-index.raw-events.json");
        var linkedStoryIndexPath = Path.Combine(runtimeIndexDirectory, "story-index.linked.json");
        var evaluatedStatePath = Path.Combine(runtimeStateDirectory, "story-state.evaluated.json");

        var modScanReport = JsonSerializer.Deserialize<ModScanReport>(
            File.ReadAllText(modsPath),
            JsonExportOptions.Default
        ) ?? new ModScanReport();

        var dialogueIndex = JsonSerializer.Deserialize<DialogueIndex>(
            File.ReadAllText(dialogueIndexPath),
            JsonExportOptions.Default
        ) ?? new DialogueIndex();

        var eventChoiceIndex = JsonSerializer.Deserialize<EventScriptChoiceIndex>(
            File.ReadAllText(eventChoiceIndexPath),
            JsonExportOptions.Default
        ) ?? new EventScriptChoiceIndex();

        var rawStoryIndex = new EventIndexBuilder().Build(modScanReport.Mods);
        var linkedStoryIndex = new StoryNodeDialogueLinker().Link(rawStoryIndex, dialogueIndex, eventChoiceIndex);

        File.WriteAllText(rawStoryIndexPath, JsonSerializer.Serialize(rawStoryIndex, JsonExportOptions.Default));
        File.WriteAllText(linkedStoryIndexPath, JsonSerializer.Serialize(linkedStoryIndex, JsonExportOptions.Default));

        if (File.Exists(evaluatedStatePath))
        {
            var currentEvaluation = JsonSerializer.Deserialize<StoryStateEvaluationReport>(
                File.ReadAllText(evaluatedStatePath),
                JsonExportOptions.Default
            ) ?? new StoryStateEvaluationReport();

            var rebuiltEvaluation = new StoryStateEvaluator().Evaluate(
                linkedStoryIndex.Nodes,
                currentEvaluation.RuntimeState,
                null,
                linkedStoryIndex.ModConfigByUniqueId);
            File.WriteAllText(evaluatedStatePath, JsonSerializer.Serialize(rebuiltEvaluation, JsonExportOptions.Default));

            var targetNode = rebuiltEvaluation.Nodes.FirstOrDefault(
                node => string.Equals(node.EventId, "MaggSebIntroduction08142025", StringComparison.Ordinal)
            );

            Console.WriteLine($"Rebuilt raw event count: {rawStoryIndex.NodeCount}");
            Console.WriteLine($"Rebuilt linked event count: {linkedStoryIndex.NodeCount}");
            Console.WriteLine(
                targetNode is null
                    ? "Target node MaggSebIntroduction08142025 not found in evaluated report."
                    : $"Target node MaggSebIntroduction08142025 status: {targetNode.Status} | {targetNode.StatusReason}"
            );

            return 0;
        }

        Console.WriteLine($"Rebuilt raw event count: {rawStoryIndex.NodeCount}");
        Console.WriteLine($"Rebuilt linked event count: {linkedStoryIndex.NodeCount}");
        Console.WriteLine("Evaluated report was not updated because story-state.evaluated.json does not exist.");
        return 0;
    }
}
