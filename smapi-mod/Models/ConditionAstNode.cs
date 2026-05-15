namespace StardewStoryInspector.Models;

public sealed class ConditionAstNode
{
    public string Type { get; init; } = "Unknown";

    public string? AtomType { get; init; }

    public string? Raw { get; init; }

    public List<string> Values { get; init; } = new();

    public List<ConditionAstNode> Children { get; init; } = new();

    public ConditionAstNode? Operand { get; init; }

    public string SourceKind { get; init; } = string.Empty;

    public string SourceFile { get; init; } = string.Empty;

    public string ExpandedFrom { get; init; } = string.Empty;
}
