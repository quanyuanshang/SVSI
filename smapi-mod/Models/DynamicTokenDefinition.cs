namespace StardewStoryInspector.Models;

public sealed class DynamicTokenDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string SourceFile { get; init; } = string.Empty;

    public List<PatchWhenCondition> WhenConditions { get; init; } = new();

    public List<string> WhenFragments { get; init; } = new();
}
