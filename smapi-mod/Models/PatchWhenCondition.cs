namespace StardewStoryInspector.Models;

public sealed class PatchWhenCondition
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string RawValue { get; init; } = string.Empty;

    public bool IsKnown { get; init; }

    public bool? Passed { get; init; }

    public bool IsProgressionSensitive { get; init; }

    public string Reason { get; init; } = string.Empty;
}
