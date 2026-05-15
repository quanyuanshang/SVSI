namespace StardewStoryInspector.Models;

public sealed class StoryStateEvaluationReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public RuntimeGameState RuntimeState { get; init; } = new();

    public TranslationCatalog TranslationCatalog { get; init; } = new();

    public int TotalNodeCount { get; init; }

    public Dictionary<string, int> StatusCounts { get; init; } = new(StringComparer.Ordinal);

    public List<UnknownConditionSummary> UnknownConditions { get; init; } = new();

    public List<StoryNodeEvaluation> Nodes { get; init; } = new();
}
