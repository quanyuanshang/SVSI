namespace StardewStoryInspector.Models;

public sealed class ExportState
{
    public int Year { get; init; }

    public string Season { get; init; } = string.Empty;

    public int Day { get; init; }

    public int Time { get; init; }

    public string Weather { get; init; } = string.Empty;

    public string PlayerName { get; init; } = string.Empty;
}
