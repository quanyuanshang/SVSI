using System.Text.Json.Nodes;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventIndexBuilderTests
{
    public static void RunAll()
    {
        Build_ExtractsTwoNodes_FromMockContentJson();
        Build_ExtractsNodes_FromIncludedChanges();
    }

    private static void Build_ExtractsTwoNodes_FromMockContentJson()
    {
        var testDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");
        var contentJsonPath = Path.Combine(testDataDirectory, "mock-content.json");

        var scannedMod = new ScannedMod
        {
            DirectoryPath = testDataDirectory,
            ManifestPath = Path.Combine(testDataDirectory, "manifest.json"),
            Name = "Mock Content Pack",
            UniqueID = "Tests.MockContentPack",
            Author = "Tests",
            Version = "1.0.0",
            ContentPackFor = new ContentPackReference
            {
                UniqueID = "Pathoschild.ContentPatcher"
            },
            IsContentPatcherContentPack = true,
            ContentJsonPath = contentJsonPath,
            ContentJson = JsonNode.Parse(File.ReadAllText(contentJsonPath))
        };

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(2, result.NodeCount, "Expected exactly 2 extracted event nodes.");

        var townNode = result.Nodes.Single(node => node.AssetTarget == "Data/Events/Town");
        AssertEqual("Town", townNode.Location, "Town node location mismatch.");
        AssertEqual("100", townNode.EventId, "Town node eventId mismatch.");
        AssertEqual("100/f Abigail 1000", townNode.RawKey, "Town node rawKey mismatch.");
        AssertSequenceEqual(
            new[] { "f Abigail 1000" },
            townNode.RawPreconditions,
            "Town node raw preconditions mismatch."
        );
        AssertEqual("AllOf", townNode.ConditionAst.Type, "Town node conditionAst root type mismatch.");
        AssertEqual(1, townNode.ConditionAst.Children.Count, "Town node conditionAst child count mismatch.");
        AssertEqual("Atom", townNode.ConditionAst.Children[0].Type, "Town node atom type mismatch.");
        AssertEqual("Friendship", townNode.ConditionAst.Children[0].AtomType, "Town node atom kind mismatch.");
        AssertSequenceEqual(
            new[] { "Abigail", "1000" },
            townNode.ConditionAst.Children[0].Values,
            "Town node friendship atom values mismatch."
        );
        AssertEqual(0, townNode.UnknownFragments.Count, "Town node should not have unknown fragments.");
        AssertEqual("Tests.MockContentPack", townNode.SourceModId, "Town node sourceModId mismatch.");
        AssertTrue(
            townNode.RawScriptPreview.Contains("speak Abigail", StringComparison.Ordinal),
            "Town node preview should include event script text."
        );

        var beachNode = result.Nodes.Single(node => node.AssetTarget == "Data/Events/Beach");
        AssertEqual("Beach", beachNode.Location, "Beach node location mismatch.");
        AssertEqual("200", beachNode.EventId, "Beach node eventId mismatch.");
        AssertEqual("200/e 10", beachNode.RawKey, "Beach node rawKey mismatch.");
        AssertSequenceEqual(
            new[] { "e 10" },
            beachNode.RawPreconditions,
            "Beach node raw preconditions mismatch."
        );
        AssertEqual("AllOf", beachNode.ConditionAst.Type, "Beach node conditionAst root type mismatch.");
        AssertEqual("Atom", beachNode.ConditionAst.Children[0].Type, "Beach node atom type mismatch.");
        AssertEqual("SawEvent", beachNode.ConditionAst.Children[0].AtomType, "Beach node atom kind mismatch.");
        AssertSequenceEqual(
            new[] { "10" },
            beachNode.ConditionAst.Children[0].Values,
            "Beach node saw-event atom values mismatch."
        );
        AssertEqual(0, beachNode.UnknownFragments.Count, "Beach node should not have unknown fragments.");
        AssertEqual(2, beachNode.EvidenceRefs.Count, "Load-based node should have 2 evidence refs.");
        AssertTrue(
            beachNode.EvidenceRefs.Any(refItem => refItem.Kind == "load-file-entry"),
            "Load-based node should include a load-file-entry evidence ref."
        );
    }

    private static void Build_ExtractsNodes_FromIncludedChanges()
    {
        var testDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");
        var contentJsonPath = Path.Combine(testDataDirectory, "mock-include-content.json");

        var scannedMod = new ScannedMod
        {
            DirectoryPath = testDataDirectory,
            ManifestPath = Path.Combine(testDataDirectory, "manifest.json"),
            Name = "Mock Include Content Pack",
            UniqueID = "Tests.MockIncludeContentPack",
            Author = "Tests",
            Version = "1.0.0",
            ContentPackFor = new ContentPackReference
            {
                UniqueID = "Pathoschild.ContentPatcher"
            },
            IsContentPatcherContentPack = true,
            ContentJsonPath = contentJsonPath,
            ContentJson = JsonNode.Parse(File.ReadAllText(contentJsonPath))
        };

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(2, result.NodeCount, "Expected exactly 2 extracted nodes from included changes.");

        var mountainNode = result.Nodes.Single(node => node.AssetTarget == "Data/Events/Mountain");
        AssertEqual("maggIntro", mountainNode.EventId, "Included edit-data node eventId mismatch.");
        AssertEqual("Mountain", mountainNode.Location, "Included edit-data node location mismatch.");
        AssertEqual(
            Path.Combine(testDataDirectory, "assets", "include-events.json"),
            mountainNode.EvidenceRefs[0].SourcePath,
            "Included edit-data node should point back to the include file."
        );

        var forestNode = result.Nodes.Single(node => node.AssetTarget == "Data/Events/Forest");
        AssertEqual("forestEvent", forestNode.EventId, "Included load node eventId mismatch.");
        AssertEqual("Forest", forestNode.Location, "Included load node location mismatch.");
        AssertEqual(2, forestNode.EvidenceRefs.Count, "Included load node should have 2 evidence refs.");
        AssertTrue(
            forestNode.EvidenceRefs.Any(refItem => refItem.SourcePath.EndsWith("nested-forest-events.json", StringComparison.OrdinalIgnoreCase)),
            "Included load node should point to the nested load file."
        );
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

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected: [{string.Join(", ", expected)}]; Actual: [{string.Join(", ", actual)}]"
            );
        }
    }
}
