using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class RuntimeStateCollector
{
    public RuntimeGameState? Collect()
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        var game1Type = typeof(Game1);
        var season = GetStaticString(game1Type, "currentSeason");
        var dayOfMonth = GetStaticInt(game1Type, "dayOfMonth");
        var player = GetStaticMemberValue(game1Type, "player");
        var currentLocation = GetStaticMemberValue(game1Type, "currentLocation");

        return new RuntimeGameState
        {
            Year = GetStaticInt(game1Type, "year"),
            Season = season,
            DayOfMonth = dayOfMonth,
            DayOfWeek = ComputeDayOfWeek(season, dayOfMonth),
            Time = GetStaticInt(game1Type, "timeOfDay"),
            Weather = DetermineWeather(game1Type),
            CurrentLocation = GetLocationName(currentLocation),
            PlayerName = GetStringMemberValue(player, "Name", "name"),
            FriendshipPoints = CollectFriendshipPoints(player),
            SeenEvents = CollectStringSet(player, "eventsSeen"),
            Mail = CollectStringSet(player, "mailReceived"),
            DialogueAnswers = CollectStringSet(
                player,
                "dialogueQuestionsAnswered",
                "dialogueQuestionsAnsweredThisSeason",
                "dialogueAnswers"
            )
        };
    }

    private static Dictionary<string, int> CollectFriendshipPoints(object? player)
    {
        var results = new Dictionary<string, int>(StringComparer.Ordinal);
        var friendshipData = GetMemberValue(player, "friendshipData");
        if (friendshipData is not IEnumerable entries)
        {
            return results;
        }

        foreach (var entry in entries)
        {
            var key = ConvertToString(UnwrapNetValue(GetMemberValue(entry, "Key")));
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var friendship = UnwrapNetValue(GetMemberValue(entry, "Value"));
            var points = ExtractInt(friendship, "Points", "points");
            if (points is not null)
            {
                results[key] = points.Value;
            }
        }

        return results;
    }

    private static HashSet<string> CollectStringSet(object? source, params string[] candidateMemberNames)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        object? collection = null;

        foreach (var memberName in candidateMemberNames)
        {
            collection = GetMemberValue(source, memberName);
            if (collection is not null)
            {
                break;
            }
        }

        if (collection is not IEnumerable enumerable)
        {
            return results;
        }

        foreach (var item in enumerable)
        {
            var value = ConvertToString(UnwrapNetValue(item));
            if (!string.IsNullOrWhiteSpace(value))
            {
                results.Add(value);
            }
        }

        return results;
    }

    private static string DetermineWeather(Type game1Type)
    {
        if (GetStaticBool(game1Type, "isLightning"))
        {
            return "storm";
        }

        if (GetStaticBool(game1Type, "isRaining"))
        {
            return "rainy";
        }

        if (GetStaticBool(game1Type, "isSnowing"))
        {
            return "snowy";
        }

        if (GetStaticBool(game1Type, "isDebrisWeather"))
        {
            return "windy";
        }

        return "sunny";
    }

    private static string ComputeDayOfWeek(string season, int dayOfMonth)
    {
        var seasonIndex = season.ToLowerInvariant() switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => 0
        };

        var weekdays = new[]
        {
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
            "Sunday"
        };

        var safeDayOfMonth = Math.Max(dayOfMonth, 1);
        var weekdayIndex = ((seasonIndex * 28) + (safeDayOfMonth - 1)) % weekdays.Length;
        return weekdays[weekdayIndex];
    }

    private static string GetLocationName(object? location)
    {
        return GetStringMemberValue(location, "NameOrUniqueName", "Name", "name");
    }

    private static string GetStaticString(Type type, string memberName)
    {
        return ConvertToString(GetStaticMemberValue(type, memberName));
    }

    private static int GetStaticInt(Type type, string memberName)
    {
        return ExtractInt(GetStaticMemberValue(type, memberName), "Value") ?? 0;
    }

    private static bool GetStaticBool(Type type, string memberName)
    {
        var value = GetStaticMemberValue(type, memberName);
        if (value is bool booleanValue)
        {
            return booleanValue;
        }

        var wrappedValue = GetMemberValue(value, "Value");
        return wrappedValue is bool wrappedBoolean && wrappedBoolean;
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var property = type.GetProperty(memberName, flags);
        if (property is not null)
        {
            return property.GetValue(null);
        }

        var field = type.GetField(memberName, flags);
        if (field is not null)
        {
            return field.GetValue(null);
        }

        return null;
    }

    private static object? GetMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = instance.GetType();

        var property = type.GetProperty(memberName, flags);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        var field = type.GetField(memberName, flags);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

        return null;
    }

    private static string GetStringMemberValue(object? instance, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
        {
            var value = GetMemberValue(instance, memberName);
            if (value is not null)
            {
                return ConvertToString(UnwrapNetValue(value));
            }
        }

        return string.Empty;
    }

    private static object? UnwrapNetValue(object? value)
    {
        if (value is null || value is string)
        {
            return value;
        }

        var nestedValue = GetMemberValue(value, "Value");
        return nestedValue ?? value;
    }

    private static int? ExtractInt(object? source, params string[] memberNames)
    {
        if (source is null)
        {
            return null;
        }

        if (source is int directInt)
        {
            return directInt;
        }

        foreach (var memberName in memberNames)
        {
            var memberValue = GetMemberValue(source, memberName);
            if (memberValue is null)
            {
                continue;
            }

            if (memberValue is int memberInt)
            {
                return memberInt;
            }

            var unwrappedMemberValue = UnwrapNetValue(memberValue);
            if (unwrappedMemberValue is int unwrappedInt)
            {
                return unwrappedInt;
            }
        }

        return int.TryParse(ConvertToString(source), out var parsedValue)
            ? parsedValue
            : null;
    }

    private static string ConvertToString(object? value)
    {
        return value?.ToString() ?? string.Empty;
    }
}
