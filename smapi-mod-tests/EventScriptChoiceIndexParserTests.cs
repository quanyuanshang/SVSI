using System.Text.Json.Nodes;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventScriptChoiceIndexParserTests
{
    public static void RunAll()
    {
        Build_ExtractsQuestionAndResponseIds_FromEventScripts();
    }

    private static void Build_ExtractsQuestionAndResponseIds_FromEventScripts()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "StardewStoryInspector-ChoiceIndexTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var contentJsonPath = Path.Combine(tempDirectory, "content.json");
            var contentJson = @"
            {
              ""Changes"": [
                {
                  ""Action"": ""EditData"",
                  ""Target"": ""Data/Events/Town"",
                  ""Entries"": {
                    ""9000/q ShaneAnswer1"": ""speak Shane \""$q ShaneAnswer1/OtherQuestion null#Question?#$r ShaneAnswer1 0 accepted#Yes#$r ShaneAnswer2 -10 rejected#No\""/end"",
                    ""9001/f Shane 2000"": ""speak Shane \""Nothing to index here.\""/end""
                  }
                }
              ]
            }
            ";

            File.WriteAllText(contentJsonPath, contentJson);

            var scannedMod = new ScannedMod
            {
                DirectoryPath = tempDirectory,
                ManifestPath = Path.Combine(tempDirectory, "manifest.json"),
                Name = "Mock Choice Pack",
                UniqueID = "Tests.MockChoicePack",
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

            var result = new EventScriptChoiceIndexParser().Build(new[] { scannedMod });

            AssertEqual(1, result.EntryCount, "Expected exactly 1 indexed event-script choice entry.");

            var entry = result.Entries.Single();
            AssertEqual("9000", entry.EventId, "Choice entry eventId mismatch.");
            AssertEqual("Town", entry.Location, "Choice entry location mismatch.");
            AssertEqual("9000/q ShaneAnswer1", entry.RawKey, "Choice entry rawKey mismatch.");
            AssertSequenceEqual(
                new[] { "ShaneAnswer1", "OtherQuestion" },
                entry.QuestionIds,
                "Choice entry question ids mismatch."
            );
            AssertSequenceEqual(
                new[] { "ShaneAnswer1", "ShaneAnswer2" },
                entry.ResponseIds,
                "Choice entry response ids mismatch."
            );
            AssertTrue(
                entry.EvidenceRefs.Any(refItem => refItem.Kind == "content-json-entry"),
                "Choice entry should include content-json-entry evidence."
            );
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
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
