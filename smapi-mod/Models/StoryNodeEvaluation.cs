namespace StardewStoryInspector.Models;

public sealed class StoryNodeEvaluation
{
    public string NodeId { get; init; } = string.Empty;

    public string EventId { get; init; } = string.Empty;

    public string SourceModId { get; init; } = string.Empty;

    public string SourceModName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string RawKey { get; init; } = string.Empty;

    public List<string> RawPreconditions { get; init; } = new();

    public List<string> UnknownFragments { get; init; } = new();

    public string RawScriptPreview { get; init; } = string.Empty;

    public List<PatchWhenCondition> PatchWhenConditions { get; init; } = new();

    public StoryNodeStatus Status { get; init; } = StoryNodeStatus.Unknown;

    public string StatusReason { get; init; } = string.Empty;

    public ConditionEvaluationResult ConditionResult { get; init; } = new();

    public List<EvidenceRef> EvidenceRefs { get; init; } = new();

    public List<RelatedDialogueRef> RelatedDialogueRefs { get; init; } = new();

    public List<RelatedEventChoiceRef> RelatedEventChoiceRefs { get; init; } = new();
}
