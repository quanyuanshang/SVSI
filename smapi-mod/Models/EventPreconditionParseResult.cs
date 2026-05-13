namespace StardewStoryInspector.Models;

public sealed class EventPreconditionParseResult
{
    public ConditionAstNode ConditionAst { get; init; } = new()
    {
        Type = "AllOf"
    };

    public List<string> UnknownFragments { get; init; } = new();
}
