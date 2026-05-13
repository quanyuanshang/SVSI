namespace StardewStoryInspector.Models;

public sealed class GameDateSnapshot
{
    public int Year { get; init; }

    public string Season { get; init; } = string.Empty;

    public int DayOfMonth { get; init; }

    public int Time { get; init; }
}
