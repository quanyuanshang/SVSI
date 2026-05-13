namespace StardewStoryInspector.Models;

public sealed class ConditionEvaluationResult
{
    public bool? Passed { get; init; }

    public bool HasUnknown { get; init; }

    public string Reason { get; init; } = string.Empty;

    public List<ConditionAtomResult> AtomResults { get; init; } = new();
}
