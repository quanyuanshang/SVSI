using System.Text.Json;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class StoryNodeDialogueLinker
{
    public StoryRawEventIndex Link(string storyIndexPath, string dialogueIndexPath)
    {
        return this.Link(storyIndexPath, dialogueIndexPath, null);
    }

    public StoryRawEventIndex Link(string storyIndexPath, string dialogueIndexPath, string? eventScriptChoiceIndexPath)
    {
        var storyIndex = JsonSerializer.Deserialize<StoryRawEventIndex>(
            File.ReadAllText(storyIndexPath),
            JsonExportOptions.Default
        ) ?? new StoryRawEventIndex();

        var dialogueIndex = JsonSerializer.Deserialize<DialogueIndex>(
            File.ReadAllText(dialogueIndexPath),
            JsonExportOptions.Default
        ) ?? new DialogueIndex();

        var eventScriptChoiceIndex = string.IsNullOrWhiteSpace(eventScriptChoiceIndexPath) || !File.Exists(eventScriptChoiceIndexPath)
            ? new EventScriptChoiceIndex()
            : JsonSerializer.Deserialize<EventScriptChoiceIndex>(
                File.ReadAllText(eventScriptChoiceIndexPath),
                JsonExportOptions.Default
            ) ?? new EventScriptChoiceIndex();

        return this.Link(storyIndex, dialogueIndex, eventScriptChoiceIndex);
    }

    public StoryRawEventIndex Link(StoryRawEventIndex storyIndex, DialogueIndex dialogueIndex)
    {
        return this.Link(storyIndex, dialogueIndex, new EventScriptChoiceIndex());
    }

    public StoryRawEventIndex Link(
        StoryRawEventIndex storyIndex,
        DialogueIndex dialogueIndex,
        EventScriptChoiceIndex eventScriptChoiceIndex)
    {
        var linkedNodes = storyIndex.Nodes
            .Select(node => this.LinkNode(node, dialogueIndex, eventScriptChoiceIndex))
            .ToList();

        return new StoryRawEventIndex
        {
            GeneratedAtUtc = storyIndex.GeneratedAtUtc,
            NodeCount = linkedNodes.Count,
            Nodes = linkedNodes
        };
    }

    private StoryNode LinkNode(
        StoryNode node,
        DialogueIndex dialogueIndex,
        EventScriptChoiceIndex eventScriptChoiceIndex)
    {
        var answerIds = ExtractChosenAnswerIds(node.ConditionAst)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var relatedDialogueRefs = answerIds
            .SelectMany(answerId => dialogueIndex.Entries
                .Where(entry => entry.ResponseIds.Contains(answerId, StringComparer.Ordinal))
                .Select(entry => new RelatedDialogueRef
                {
                    NpcName = entry.NpcName,
                    DialogueKey = entry.DialogueKey,
                    ResponseId = answerId,
                    PreviewText = entry.PreviewText,
                    SourceModId = entry.SourceModId
                }))
            .GroupBy(
                item => $"{item.SourceModId}|{item.NpcName}|{item.DialogueKey}|{item.ResponseId}",
                StringComparer.Ordinal
            )
            .Select(group => group.First())
            .ToList();

        var relatedEventChoiceRefs = answerIds
            .SelectMany(answerId => eventScriptChoiceIndex.Entries
                .Where(entry => entry.ResponseIds.Contains(answerId, StringComparer.Ordinal))
                .Select(entry => new RelatedEventChoiceRef
                {
                    EventId = entry.EventId,
                    AssetTarget = entry.AssetTarget,
                    Location = entry.Location,
                    RawKey = entry.RawKey,
                    ResponseId = answerId,
                    PreviewText = entry.PreviewText,
                    SourceModId = entry.SourceModId,
                    SourceModName = entry.SourceModName
                }))
            .GroupBy(
                item => $"{item.SourceModId}|{item.EventId}|{item.RawKey}|{item.ResponseId}",
                StringComparer.Ordinal
            )
            .Select(group => group.First())
            .ToList();

        return new StoryNode
        {
            NodeId = node.NodeId,
            EventId = node.EventId,
            SourceModId = node.SourceModId,
            SourceModName = node.SourceModName,
            AssetTarget = node.AssetTarget,
            Location = node.Location,
            RawKey = node.RawKey,
            RawPreconditions = node.RawPreconditions,
            PatchWhenConditions = node.PatchWhenConditions,
            ConditionAst = node.ConditionAst,
            UnknownFragments = node.UnknownFragments,
            RawScriptPreview = node.RawScriptPreview,
            EvidenceRefs = node.EvidenceRefs,
            RelatedDialogueRefs = relatedDialogueRefs,
            RelatedEventChoiceRefs = relatedEventChoiceRefs
        };
    }

    private static IEnumerable<string> ExtractChosenAnswerIds(ConditionAstNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        if (string.Equals(node.Type, "Atom", StringComparison.Ordinal) &&
            string.Equals(node.AtomType, "ChoseDialogueAnswers", StringComparison.Ordinal))
        {
            foreach (var value in node.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }

        if (node.Operand is not null)
        {
            foreach (var value in ExtractChosenAnswerIds(node.Operand))
            {
                yield return value;
            }
        }

        foreach (var child in node.Children)
        {
            foreach (var value in ExtractChosenAnswerIds(child))
            {
                yield return value;
            }
        }
    }
}
