namespace StardewStoryInspector.Models;

public sealed class RuntimeStateExport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public RuntimeGameState State { get; init; } = new();
}
