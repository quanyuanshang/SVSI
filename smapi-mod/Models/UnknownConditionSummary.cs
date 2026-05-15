namespace StardewStoryInspector.Models;

public sealed class UnknownConditionSummary
{
    public string Raw { get; init; } = string.Empty;

    public int Count { get; init; }

    public List<string> SourceFiles { get; init; } = new();

    public List<string> ExampleEvents { get; init; } = new();

    public string SuggestedParserType { get; init; } = string.Empty;
}
