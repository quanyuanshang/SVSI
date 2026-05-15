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
        Build_CapturesPatchWhenConditions();
        Build_ExpandsDynamicTokenPreconditions_WithoutMarkingUnknown();
        Build_CollectsDynamicTokensFromIncludes();
        Build_IgnoresNonStringDynamicTokenValuesWithoutThrowing();
        Build_ResolvesMinFriendshipDynamicTokenFromConfigQuery();
        Build_SkipsBranchOnlyEventEntries();
        Build_DeduplicatesOverriddenEventKeysWithinLocation();
        Build_SkipsAnswerIdAfterForkEventIdAnswerId();
    }

    private static void Build_SkipsAnswerIdAfterForkEventIdAnswerId()
    {
        var scannedMod = CreateScannedMod(
            "Fork TwoArg Pack",
            "Tests.ForkTwoArgPack",
            "{ \"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/FarmHouse\", \"Entries\": { " +
            "\"626070601/t 2000 2600\": \"continue/fork 626070612 rainynight_leave/pause 500/fork 626070621 rainynight_explain/end\", " +
            "\"rainynight_leave\": \"pause 500/end\", " +
            "\"rainynight_explain\": \"pause 500/end\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(1, result.NodeCount, "Two-arg fork answer-ids must be treated as branch-only and skipped.");
        AssertEqual("626070601", result.Nodes.Single().EventId, "Only the parent event should remain indexed.");
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

    private static void Build_CapturesPatchWhenConditions()
    {
        var scannedMod = CreateScannedMod(
            "When Pack",
            "Tests.WhenPack",
            "{ \"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/Farm\", \"When\": { \"Relationship:Alex\": \"Engaged\" }, \"Entries\": { \"100/t 600 2400\": \"event script\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });
        var node = result.Nodes.Single();

        AssertEqual(1, node.PatchWhenConditions.Count, "Patch When condition should be captured.");
        AssertEqual("Relationship:Alex", node.PatchWhenConditions[0].Key, "Patch When key mismatch.");
        AssertEqual("Engaged", node.PatchWhenConditions[0].Value, "Patch When value mismatch.");
        AssertTrue(!node.PatchWhenConditions[0].IsKnown, "Patch When should be marked unknown until evaluated.");
    }

    private static void Build_ExpandsDynamicTokenPreconditions_WithoutMarkingUnknown()
    {
        var scannedMod = CreateScannedMod(
            "Dynamic Token Pack",
            "Tests.DynamicTokenPack",
            "{ \"DynamicTokens\": [ " +
            "{ \"Name\": \"CampoutDays\", \"Value\": \"Season spring/u 12 19 20\", \"When\": { \"Season\": \"spring\" } }, " +
            "{ \"Name\": \"CampoutDays\", \"Value\": \"!Season summer\", \"When\": { \"Season\": \"summer\" } }, " +
            "{ \"Name\": \"CampoutDays\", \"Value\": \"Season fall/u 13 14 18\", \"When\": { \"Season\": \"fall\" } }, " +
            "{ \"Name\": \"CampoutDays\", \"Value\": \"!Season winter\", \"When\": { \"Season\": \"winter\" } } " +
            "], \"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/Forest\", \"Entries\": { \"700001/{{CampoutDays}}\": \"event script\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });
        var node = result.Nodes.Single();

        AssertEqual(0, node.UnknownFragments.Count, "Dynamic token precondition should not be treated as unknown.");
        AssertSequenceEqual(new[] { "{{CampoutDays}}" }, node.RawPreconditions, "Raw preconditions should preserve the original token.");
        AssertEqual("AnyOf", node.ConditionAst.Children.Single().Type, "CampoutDays should expand into seasonal alternatives.");
    }

    private static void Build_CollectsDynamicTokensFromIncludes()
    {
        var scannedMod = CreateScannedMod(
            "Included Dynamic Tokens",
            "Tests.IncludedDynamicTokens",
            "{ \"Changes\": [ " +
            "{ \"Action\": \"Include\", \"FromFile\": \"tokens.json\" }, " +
            "{ \"Action\": \"EditData\", \"Target\": \"Data/Events/Forest\", \"Entries\": { " +
            "\"710001/{{FrogDays}}\": \"event script\", " +
            "\"710002/{{MineDays}}\": \"event script\", " +
            "\"710003/{{OverlookDays}}\": \"event script\", " +
            "\"710004/{{PoolDays}}\": \"event script\", " +
            "\"710005/{{CampoutDays}}\": \"event script\" " +
            "} } ] }"
        );
        File.WriteAllText(
            Path.Combine(scannedMod.DirectoryPath, "tokens.json"),
            "{ \"DynamicTokens\": [ " +
            "{ \"Name\": \"FrogDays\", \"Value\": \"Season spring/u 1\" }, " +
            "{ \"Name\": \"MineDays\", \"Value\": \"Season summer/u 2\" }, " +
            "{ \"Name\": \"OverlookDays\", \"Value\": \"Season fall/u 3\" }, " +
            "{ \"Name\": \"PoolDays\", \"Value\": \"Season winter/u 4\" }, " +
            "{ \"Name\": \"CampoutDays\", \"Value\": \"Season spring/u 12 19 20\" } " +
            "] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(5, result.NodeCount, "Every included DynamicToken event should be indexed.");
        AssertTrue(result.Nodes.All(node => node.UnknownFragments.Count == 0), "Included DynamicToken refs should not become unknown.");
        AssertTrue(
            result.Nodes.All(node => node.ConditionAst.Children.Single().Type != "Unknown"),
            "Included DynamicToken refs should expand into parsed conditions."
        );
    }

    private static void Build_IgnoresNonStringDynamicTokenValuesWithoutThrowing()
    {
        var scannedMod = CreateScannedMod(
            "Boolean Dynamic Token",
            "Tests.BooleanDynamicToken",
            "{ \"DynamicTokens\": [ { \"Name\": \"SkipMe\", \"Value\": false } ], " +
            "\"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/Farm\", \"Entries\": { \"720001/t 600 700\": \"event script\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(1, result.NodeCount, "Boolean-valued DynamicTokens should not crash event indexing.");
    }

    private static void Build_ResolvesMinFriendshipDynamicTokenFromConfigQuery()
    {
        var scannedMod = CreateScannedMod(
            "Date Config Pack",
            "Tests.DateConfigPack",
            "{ \"DynamicTokens\": [ { \"Name\": \"MinFriendship\", \"Value\": \"{{Query: {{Min Hearts Required}} * 250}}\" } ], " +
            "\"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/WizardHouse\", \"Entries\": { \"730001/f Wizard {{MinFriendship}}\": \"event script\" } } ] }"
        );
        scannedMod.ConfigValues["Min Hearts Required"] = "10";

        var result = new EventIndexBuilder().Build(new[] { scannedMod });
        var node = result.Nodes.Single();
        var atom = node.ConditionAst.Children.Single();

        AssertEqual(0, node.UnknownFragments.Count, "Resolved MinFriendship should not become an unknown fragment.");
        AssertEqual("Friendship", atom.AtomType, "MinFriendship should still parse as a friendship atom.");
        AssertSequenceEqual(new[] { "Wizard", "2500" }, atom.Values, "MinFriendship should resolve from config query math.");
    }

    private static void Build_SkipsBranchOnlyEventEntries()
    {
        var scannedMod = CreateScannedMod(
            "Branch Pack",
            "Tests.BranchPack",
            "{ \"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/Farm\", \"Entries\": { \"100/t 600 2400\": \"question fork0 \\\"Go?#Yes#No\\\"/fork dateYes/end\", \"dateYes\": \"pause 200/end\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });

        AssertEqual(1, result.NodeCount, "Branch-only fork target should not be indexed as a normal event.");
        AssertEqual("100", result.Nodes.Single().EventId, "Only the entry event should remain.");
    }

    private static void Build_DeduplicatesOverriddenEventKeysWithinLocation()
    {
        var scannedMod = CreateScannedMod(
            "Override Pack",
            "Tests.OverridePack",
            "{ \"Changes\": [ { \"Action\": \"EditData\", \"Target\": \"Data/Events/Farm\", \"Entries\": { \"908070/t 600 900\": \"old script\" } }, { \"Action\": \"EditData\", \"Target\": \"Data/Events/Farm\", \"Entries\": { \"908070/t 600 900\": \"new script\" } } ] }"
        );

        var result = new EventIndexBuilder().Build(new[] { scannedMod });
        var node = result.Nodes.Single();

        AssertEqual(1, result.NodeCount, "Duplicate location/raw-key entries should collapse to one node.");
        AssertTrue(node.RawScriptPreview.Contains("new script", StringComparison.Ordinal), "The later patch entry should win.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static ScannedMod CreateScannedMod(string name, string uniqueId, string contentJson)
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(EventIndexBuilderTests),
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(testDirectory);
        var contentJsonPath = Path.Combine(testDirectory, "content.json");
        File.WriteAllText(contentJsonPath, contentJson);

        return new ScannedMod
        {
            DirectoryPath = testDirectory,
            ManifestPath = Path.Combine(testDirectory, "manifest.json"),
            Name = name,
            UniqueID = uniqueId,
            Author = "Tests",
            Version = "1.0.0",
            ContentPackFor = new ContentPackReference
            {
                UniqueID = "Pathoschild.ContentPatcher"
            },
            IsContentPatcherContentPack = true,
            ContentJsonPath = contentJsonPath,
            ContentJson = JsonNode.Parse(contentJson)
        };
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
