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

    public HashSet<string> InstalledModIds { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> FriendshipPoints { get; init; } = new(StringComparer.Ordinal);

    public string? SpouseName { get; init; }

    public string? Spouse { get; init; }

    public string? MarriedTo { get; init; }

    public string[]? Spouses { get; init; }

    public string? EngagedTo { get; init; }

    public string? Roommate { get; init; }

    public HashSet<string> DatingNpcNames { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> VisibleNpcNamesHere { get; init; } = new(StringComparer.Ordinal);

    public bool? InUpgradedHouse { get; init; }

    public HashSet<string> SeenEvents { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> Mail { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> DialogueAnswers { get; init; } = new(StringComparer.Ordinal);
}
