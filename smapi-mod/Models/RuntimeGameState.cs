namespace StardewStoryInspector.Models;

public sealed class RuntimeGameState
{
    public int Year { get; init; }

    public string Season { get; init; } = string.Empty;

    public int DayOfMonth { get; init; }

    public string DayOfWeek { get; init; } = string.Empty;

    public int Time { get; init; }

    public string Weather { get; init; } = string.Empty;

    public string CurrentLocation { get; init; } = string.Empty;

    public string PlayerName { get; init; } = string.Empty;

    public Dictionary<string, int> FriendshipPoints { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> SeenEvents { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> Mail { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> DialogueAnswers { get; init; } = new(StringComparer.Ordinal);
}
