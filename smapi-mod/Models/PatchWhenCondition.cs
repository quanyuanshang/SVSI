namespace StardewStoryInspector.Models;

public sealed class PatchWhenCondition
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string RawValue { get; init; } = string.Empty;

    public bool IsKnown { get; init; }

    public bool? Passed { get; init; }

    public bool IsContextSensitive { get; init; }

    public bool IsProgressionSensitive { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>parseUnknown | runtimeMissing | complexQueryUnsupported | externalTokenMissing</summary>
    public string UnknownKind { get; init; } = string.Empty;

    public string ReasonZh { get; init; } = string.Empty;

    public string ParsedType { get; init; } = string.Empty;
}
