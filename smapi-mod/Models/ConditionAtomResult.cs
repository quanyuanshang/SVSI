namespace StardewStoryInspector.Models;

public sealed class ConditionAtomResult
{
    public string Raw { get; init; } = string.Empty;

    public string AtomType { get; init; } = string.Empty;

    public bool? Passed { get; init; }

    public bool IsContextSensitive { get; init; }

    public bool IsProgressionSensitive { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Optional: externalTokenMissing | runtimeMissing for display-only Zh mapping.</summary>
    public string? UnknownKind { get; init; }

    public string? ReasonZh { get; init; }
}
