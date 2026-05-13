namespace StardewStoryInspector.Models;

public sealed class EventScriptChoiceEntry
{
    public string SourceModId { get; init; } = string.Empty;

    public string SourceModName { get; init; } = string.Empty;

    public string EventId { get; init; } = string.Empty;

    public string AssetTarget { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string RawKey { get; init; } = string.Empty;

    public string RawScript { get; init; } = string.Empty;

    public string PreviewText { get; init; } = string.Empty;

    public List<string> QuestionIds { get; init; } = new();

    public List<string> ResponseIds { get; init; } = new();

    public List<EvidenceRef> EvidenceRefs { get; init; } = new();
}
