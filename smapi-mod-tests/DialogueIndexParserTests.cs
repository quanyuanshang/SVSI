using System.Text.Json.Nodes;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class DialogueIndexParserTests
{
    public static void RunAll()
    {
        Build_ExtractsDialogueEntries_FromMockContentJson();
    }

    private static void Build_ExtractsDialogueEntries_FromMockContentJson()
    {
        var testDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");
        var contentJsonPath = Path.Combine(testDataDirectory, "mock-dialogue-content.json");

        var scannedMod = new ScannedMod
        {
            DirectoryPath = testDataDirectory,
            ManifestPath = Path.Combine(testDataDirectory, "manifest.json"),
            Name = "Mock Dialogue Pack",
            UniqueID = "Tests.MockDialoguePack",
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

        var result = new DialogueIndexParser().Build(new[] { scannedMod });

        AssertEqual(3, result.EntryCount, "Expected exactly 3 extracted dialogue entries.");

        var plainDialogue = result.Entries.Single(entry => entry.DialogueKey == "RainyDay_0");
        AssertEqual("Shane", plainDialogue.NpcName, "Plain dialogue npcName mismatch.");
        AssertEqual("Hello there.", plainDialogue.RawDialogue, "Plain dialogue raw text mismatch.");
        AssertEqual("Hello there.", plainDialogue.PreviewText, "Plain dialogue preview mismatch.");
        AssertEqual(0, plainDialogue.ResponseIds.Count, "Plain dialogue should not have response ids.");
        AssertEqual(0, plainDialogue.LinkedEventIds.Count, "Plain dialogue should not have linked event ids.");

        var responseDialogue = result.Entries.Single(entry => entry.DialogueKey == "ResponseTest");
        AssertSequenceEqual(
            new[] { "answer_yes", "answer_no" },
            responseDialogue.ResponseIds,
            "Response ids mismatch."
        );
        AssertEqual(0, responseDialogue.LinkedEventIds.Count, "Response dialogue should not have linked event ids.");

        var linkedEventDialogue = result.Entries.Single(entry => entry.DialogueKey == "LinkedEventTest");
        AssertSequenceEqual(
            new[] { "77771" },
            linkedEventDialogue.LinkedEventIds,
            "Linked event ids mismatch."
        );
        AssertEqual(0, linkedEventDialogue.ResponseIds.Count, "Linked event dialogue should not have response ids.");
        AssertTrue(
            linkedEventDialogue.EvidenceRefs.Any(refItem => refItem.Kind == "content-json-entry"),
            "Dialogue entry should include content-json-entry evidence."
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
