namespace StardewStoryInspector.Models;

public sealed class DialogueIndexEntry
{
    public string SourceModId { get; init; } = string.Empty;

    public string SourceModName { get; init; } = string.Empty;

    public string NpcName { get; init; } = string.Empty;

    public string DialogueKey { get; init; } = string.Empty;

    public string RawDialogue { get; init; } = string.Empty;

    public string PreviewText { get; init; } = string.Empty;

    public List<string> ResponseIds { get; init; } = new();

    public List<string> LinkedEventIds { get; init; } = new();

    public List<EvidenceRef> EvidenceRefs { get; init; } = new();
}
