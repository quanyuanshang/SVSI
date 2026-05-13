using System.Text.Json;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class StoryNodeDialogueLinkerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void RunAll()
    {
        Link_CreatesRelation_WhenStoryNodeDependsOnDialogueAnswer();
        Link_CreatesEventChoiceRelation_WhenStoryNodeDependsOnEventScriptAnswer();
    }

    private static void Link_CreatesRelation_WhenStoryNodeDependsOnDialogueAnswer()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "StardewStoryInspector-LinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var storyIndexPath = Path.Combine(tempDirectory, "story-index.raw-events.json");
            var dialogueIndexPath = Path.Combine(tempDirectory, "dialogue-index.json");
            var eventScriptChoiceIndexPath = Path.Combine(tempDirectory, "event-script-choice-index.json");

            var storyIndex = new StoryRawEventIndex
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                NodeCount = 1,
                Nodes =
                {
                    new StoryNode
                    {
                        NodeId = "story-node:test",
                        EventId = "9000",
                        SourceModId = "Tests.MockStoryPack",
                        SourceModName = "Mock Story Pack",
                        AssetTarget = "Data/Events/Town",
                        Location = "Town",
                        RawKey = "9000/q ShaneAnswer1",
                        RawPreconditions = new List<string> { "q ShaneAnswer1" },
                        ConditionAst = new ConditionAstNode
                        {
                            Type = "AllOf",
                            Children =
                            {
                                new ConditionAstNode
                                {
                                    Type = "Atom",
                                    AtomType = "ChoseDialogueAnswers",
                                    Raw = "q ShaneAnswer1",
                                    Values = new List<string> { "ShaneAnswer1" }
                                }
                            }
                        },
                        RawScriptPreview = "Test preview"
                    }
                }
            };

            var dialogueIndex = new DialogueIndex
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                EntryCount = 1,
                Entries =
                {
                    new DialogueIndexEntry
                    {
                        SourceModId = "Tests.MockDialoguePack",
                        SourceModName = "Mock Dialogue Pack",
                        NpcName = "Shane",
                        DialogueKey = "Question_0",
                        RawDialogue = "Question?/$r ShaneAnswer1 50 Yes",
                        PreviewText = "Question?/$r ShaneAnswer1 50 Yes",
                        ResponseIds = new List<string> { "ShaneAnswer1" }
                    }
                }
            };

            var eventScriptChoiceIndex = new EventScriptChoiceIndex();

            File.WriteAllText(storyIndexPath, JsonSerializer.Serialize(storyIndex, SerializerOptions));
            File.WriteAllText(dialogueIndexPath, JsonSerializer.Serialize(dialogueIndex, SerializerOptions));
            File.WriteAllText(eventScriptChoiceIndexPath, JsonSerializer.Serialize(eventScriptChoiceIndex, SerializerOptions));

            var linked = new StoryNodeDialogueLinker().Link(
                storyIndexPath,
                dialogueIndexPath,
                eventScriptChoiceIndexPath
            );
            var node = linked.Nodes.Single();

            AssertEqual(1, node.RelatedDialogueRefs.Count, "Expected exactly 1 related dialogue ref.");
            AssertEqual(0, node.RelatedEventChoiceRefs.Count, "Did not expect event-script choice refs in dialogue-only case.");
            var linkedDialogue = node.RelatedDialogueRefs[0];
            AssertEqual("Shane", linkedDialogue.NpcName, "Related dialogue npcName mismatch.");
            AssertEqual("Question_0", linkedDialogue.DialogueKey, "Related dialogue key mismatch.");
            AssertEqual("ShaneAnswer1", linkedDialogue.ResponseId, "Related response id mismatch.");
            AssertEqual("Question?/$r ShaneAnswer1 50 Yes", linkedDialogue.PreviewText, "Related preview mismatch.");
            AssertEqual("Tests.MockDialoguePack", linkedDialogue.SourceModId, "Related sourceModId mismatch.");
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static void Link_CreatesEventChoiceRelation_WhenStoryNodeDependsOnEventScriptAnswer()
    {
        var storyIndex = new StoryRawEventIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            NodeCount = 1,
            Nodes =
            {
                new StoryNode
                {
                    NodeId = "story-node:event-choice",
                    EventId = "9001",
                    SourceModId = "Tests.MockStoryPack",
                    SourceModName = "Mock Story Pack",
                    AssetTarget = "Data/Events/Town",
                    Location = "Town",
                    RawKey = "9001/q ShaneAnswer1",
                    RawPreconditions = new List<string> { "q ShaneAnswer1" },
                    ConditionAst = new ConditionAstNode
                    {
                        Type = "AllOf",
                        Children =
                        {
                            new ConditionAstNode
                            {
                                Type = "Atom",
                                AtomType = "ChoseDialogueAnswers",
                                Raw = "q ShaneAnswer1",
                                Values = new List<string> { "ShaneAnswer1" }
                            }
                        }
                    },
                    RawScriptPreview = "Story preview"
                }
            }
        };

        var dialogueIndex = new DialogueIndex();
        var eventScriptChoiceIndex = new EventScriptChoiceIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            EntryCount = 1,
            Entries =
            {
                new EventScriptChoiceEntry
                {
                    SourceModId = "Tests.MockChoicePack",
                    SourceModName = "Mock Choice Pack",
                    EventId = "8000",
                    AssetTarget = "Data/Events/Forest",
                    Location = "Forest",
                    RawKey = "8000/f Shane 2000",
                    RawScript = "speak Shane \"$q ShaneAnswer1 null#Question?#$r ShaneAnswer1 0 answer_key#Yes\"/end",
                    PreviewText = "speak Shane \"$q ShaneAnswer1 null#Question?#$r ShaneAnswer1 0 answer_key#Yes\"/end",
                    QuestionIds = new List<string> { "ShaneAnswer1" },
                    ResponseIds = new List<string> { "ShaneAnswer1" }
                }
            }
        };

        var linked = new StoryNodeDialogueLinker().Link(storyIndex, dialogueIndex, eventScriptChoiceIndex);
        var node = linked.Nodes.Single();

        AssertEqual(0, node.RelatedDialogueRefs.Count, "Did not expect dialogue refs in event-choice-only case.");
        AssertEqual(1, node.RelatedEventChoiceRefs.Count, "Expected exactly 1 related event choice ref.");
        var relatedChoice = node.RelatedEventChoiceRefs[0];
        AssertEqual("8000", relatedChoice.EventId, "Related event choice eventId mismatch.");
        AssertEqual("Forest", relatedChoice.Location, "Related event choice location mismatch.");
        AssertEqual("8000/f Shane 2000", relatedChoice.RawKey, "Related event choice rawKey mismatch.");
        AssertEqual("ShaneAnswer1", relatedChoice.ResponseId, "Related event choice responseId mismatch.");
        AssertEqual("Tests.MockChoicePack", relatedChoice.SourceModId, "Related event choice sourceModId mismatch.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }
}
