using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class InspectSingleContentPackCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: inspect-single-content-pack <modDir> <contentJsonPath> <name> <uniqueId>");
            return 1;
        }

        var modDirectory = args[1];
        var contentJsonPath = args[2];
        var name = args[3];
        var uniqueId = args[4];

        var scannedMod = new ScannedMod
        {
            DirectoryPath = modDirectory,
            ManifestPath = Path.Combine(modDirectory, "manifest.json"),
            Name = name,
            UniqueID = uniqueId,
            Author = "Debug",
            Version = "1.0.0",
            ContentPackFor = new ContentPackReference
            {
                UniqueID = "Pathoschild.ContentPatcher"
            },
            IsContentPatcherContentPack = true,
            ContentJsonPath = contentJsonPath,
            ContentJson = LooseJsonParser.ParseNodeFromFile(contentJsonPath)
        };

        var result = new EventIndexBuilder().Build(new[] { scannedMod });
        var targetNode = result.Nodes.FirstOrDefault(node => string.Equals(node.EventId, "MaggSebIntroduction08142025", StringComparison.Ordinal));

        Console.WriteLine($"NodeCount={result.NodeCount}");
        Console.WriteLine(targetNode is null
            ? "Target node not found."
            : $"Target node found: {targetNode.EventId} | {targetNode.Location} | {targetNode.RawKey}");

        foreach (var node in result.Nodes
                     .Where(node => node.SourceModId == uniqueId)
                     .Take(30))
        {
            Console.WriteLine($"{node.EventId} | {node.Location} | {node.RawKey}");
        }

        return 0;
    }
}
