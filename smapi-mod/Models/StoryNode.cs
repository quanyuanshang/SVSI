namespace StardewStoryInspector.Models;

public sealed class StoryNode
{
    public string NodeId { get; init; } = string.Empty;

    public string EventId { get; init; } = string.Empty;

    public string SourceModId { get; init; } = string.Empty;

    public string SourceModName { get; init; } = string.Empty;

    public string AssetTarget { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string RawKey { get; init; } = string.Empty;

    public List<string> RawPreconditions { get; init; } = new();

    public ConditionAstNode ConditionAst { get; init; } = new()
    {
        Type = "AllOf"
    };

    public List<string> UnknownFragments { get; init; } = new();

    public string RawScriptPreview { get; init; } = string.Empty;

    public List<EvidenceRef> EvidenceRefs { get; init; } = new();

    public List<RelatedDialogueRef> RelatedDialogueRefs { get; init; } = new();

    public List<RelatedEventChoiceRef> RelatedEventChoiceRefs { get; init; } = new();
}
