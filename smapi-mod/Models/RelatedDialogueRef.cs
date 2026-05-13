namespace StardewStoryInspector.Models;

public sealed class RelatedDialogueRef
{
    public string NpcName { get; init; } = string.Empty;

    public string DialogueKey { get; init; } = string.Empty;

    public string ResponseId { get; init; } = string.Empty;

    public string PreviewText { get; init; } = string.Empty;

    public string SourceModId { get; init; } = string.Empty;
}
