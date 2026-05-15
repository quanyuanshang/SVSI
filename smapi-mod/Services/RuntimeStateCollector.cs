using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class RuntimeStateCollector
{
    private readonly IModRegistry? modRegistry;

    public RuntimeStateCollector(IModRegistry? modRegistry = null)
    {
        this.modRegistry = modRegistry;
    }

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
        var relationships = CollectRelationships(player);

        var dayEventsSnapshot = CollectDayEvents(game1Type);
        var activeDialogueSnapshot = CollectActiveDialogueEvents(player);
        var inUpgradedHouse = DetermineInUpgradedHouse(player, currentLocation);
        var farmhouseSnapshot = CollectFarmhouseUpgrade(player, inUpgradedHouse);
        var spouseBedSnapshot = CollectSpouseBed(relationships, inUpgradedHouse);

        return new RuntimeGameState
        {
            Year = GetStaticInt(game1Type, "year"),
            Season = season,
            DayOfMonth = dayOfMonth,
            DayOfWeek = ResolveDayOfWeek(game1Type, season, dayOfMonth),
            Time = GetStaticInt(game1Type, "timeOfDay"),
            Weather = DetermineWeather(game1Type),
            IsFestivalDay = DetermineIsFestivalDay(game1Type),
            DayEventsKnown = dayEventsSnapshot.Known,
            DayEvents = dayEventsSnapshot.Events,
            CurrentLocation = GetLocationName(currentLocation),
            PlayerName = GetStringMemberValue(player, "Name", "name"),
            InstalledModIds = CollectInstalledModIds(this.modRegistry),
            FriendshipPoints = CollectFriendshipPoints(player),
            SpouseName = relationships.SpouseName,
            Spouse = relationships.Spouse,
            MarriedTo = relationships.MarriedTo,
            Spouses = relationships.Spouses,
            EngagedTo = relationships.EngagedTo,
            Roommate = relationships.Roommate,
            DatingNpcNames = CollectDatingNpcNames(player),
            VisibleNpcNamesHere = CollectVisibleNpcNamesHere(currentLocation),
            InUpgradedHouse = inUpgradedHouse,
            FarmhouseUpgradeKnown = farmhouseSnapshot.Known,
            FarmhouseUpgradeLevel = farmhouseSnapshot.Level,
            SpouseBedKnown = spouseBedSnapshot.Known,
            HasSpouseBed = spouseBedSnapshot.HasBed,
            SeenEvents = CollectStringSet(player, "eventsSeen"),
            Mail = CollectStringSet(player, "mailReceived"),
            DialogueAnswers = CollectStringSet(
                player,
                "dialogueQuestionsAnswered",
                "dialogueQuestionsAnsweredThisSeason",
                "dialogueAnswers"
            ),
            ActiveDialogueEventsKnown = activeDialogueSnapshot.Known,
            ActiveDialogueEvents = activeDialogueSnapshot.Events
        };
    }

    private static ActiveDialogueSnapshot CollectActiveDialogueEvents(object? player)
    {
        var events = new HashSet<string>(StringComparer.Ordinal);
        var activeDialogueEvents = GetMemberValue(player, "activeDialogueEvents");
        if (activeDialogueEvents is null)
        {
            return new ActiveDialogueSnapshot { Known = false, Events = events };
        }

        if (TryCollectDictionaryKeys(activeDialogueEvents, events) || TryCollectEnumerableStrings(activeDialogueEvents, events))
        {
            return new ActiveDialogueSnapshot { Known = true, Events = events };
        }

        return new ActiveDialogueSnapshot { Known = false, Events = events };
    }

    private static bool TryCollectDictionaryKeys(object source, ISet<string> sink)
    {
        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        var added = false;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var key = ConvertToString(itemType.GetProperty("Key")?.GetValue(item));
                if (!string.IsNullOrWhiteSpace(key))
                {
                    sink.Add(key.Trim());
                    added = true;
                }
            }
        }

        return added;
    }

    private static bool TryCollectEnumerableStrings(object source, ISet<string> sink)
    {
        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        var added = false;
        foreach (var item in enumerable)
        {
            var value = ConvertToString(item);
            if (!string.IsNullOrWhiteSpace(value))
            {
                sink.Add(value.Trim());
                added = true;
            }
        }

        return added;
    }

    private sealed class ActiveDialogueSnapshot
    {
        public bool Known { get; init; }

        public HashSet<string> Events { get; init; } = new(StringComparer.Ordinal);
    }

    private static HashSet<string> CollectInstalledModIds(IModRegistry? modRegistry)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        if (modRegistry is null)
        {
            return results;
        }

        foreach (var mod in modRegistry.GetAll())
        {
            // Avoid IManifest (SMAPI.Toolkit.CoreInterfaces) — project only references
            // StardewModdingAPI.dll; read UniqueID via reflection like other runtime probes.
            var manifest = GetMemberValue(mod, "Manifest");
            var uniqueId = ConvertToString(GetMemberValue(manifest, "UniqueID"));
            if (!string.IsNullOrWhiteSpace(uniqueId))
            {
                results.Add(uniqueId.Trim());
            }
        }

        return results;
    }

    private static Dictionary<string, int> CollectFriendshipPoints(object? player)
    {
        var results = new Dictionary<string, int>(StringComparer.Ordinal);
        var friendshipData = GetMemberValue(player, "friendshipData");
        if (friendshipData is null)
        {
            return results;
        }

        // NetStringDictionary exposes the underlying runtime dict via FieldDict / Pairs;
        // depending on Stardew/NetCode version one path or another may yield empty results,
        // so probe all of them and merge whatever has entries.
        var enumerationSources = new[]
        {
            GetMemberValue(friendshipData, "FieldDict"),
            GetMemberValue(friendshipData, "Pairs"),
            friendshipData
        };

        foreach (var source in enumerationSources)
        {
            if (TryCollectFriendshipFromEnumerable(source, results) && results.Count > 0)
            {
                break;
            }
        }

        return results;
    }

    private static RelationshipState CollectRelationships(object? player)
    {
        var spouseName = GetStringMemberValue(player, "spouse", "Spouse");
        var normalizedSpouseName = NormalizeOptionalName(spouseName);
        var marriedNames = new HashSet<string>(StringComparer.Ordinal);
        var engagedNames = new HashSet<string>(StringComparer.Ordinal);
        var roommateNames = new HashSet<string>(StringComparer.Ordinal);

        if (normalizedSpouseName is not null)
        {
            marriedNames.Add(normalizedSpouseName);
        }

        var friendshipData = GetMemberValue(player, "friendshipData");
        if (friendshipData is not null)
        {
            var enumerationSources = new[]
            {
                GetMemberValue(friendshipData, "FieldDict"),
                GetMemberValue(friendshipData, "Pairs"),
                friendshipData
            };

            foreach (var source in enumerationSources)
            {
                if (TryCollectRelationshipsFromEnumerable(source, marriedNames, engagedNames, roommateNames))
                {
                    break;
                }
            }
        }

        var primarySpouse = normalizedSpouseName ?? marriedNames.FirstOrDefault();
        var spouseList = marriedNames.Count > 0
            ? marriedNames.OrderBy(static name => name, StringComparer.Ordinal).ToArray()
            : null;

        return new RelationshipState(
            SpouseName: primarySpouse,
            Spouse: primarySpouse,
            MarriedTo: primarySpouse,
            Spouses: spouseList,
            EngagedTo: engagedNames.OrderBy(static name => name, StringComparer.Ordinal).FirstOrDefault(),
            Roommate: roommateNames.OrderBy(static name => name, StringComparer.Ordinal).FirstOrDefault()
        );
    }

    private static HashSet<string> CollectDatingNpcNames(object? player)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        var friendshipData = GetMemberValue(player, "friendshipData");
        if (friendshipData is null)
        {
            return results;
        }

        var enumerationSources = new[]
        {
            GetMemberValue(friendshipData, "FieldDict"),
            GetMemberValue(friendshipData, "Pairs"),
            friendshipData
        };

        foreach (var source in enumerationSources)
        {
            if (TryCollectDatingFromEnumerable(source, results) && results.Count > 0)
            {
                break;
            }
        }

        return results;
    }

    private static HashSet<string> CollectVisibleNpcNamesHere(object? currentLocation)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        var characters = GetMemberValue(currentLocation, "characters") ?? GetMemberValue(currentLocation, "Characters");
        if (characters is not IEnumerable enumerable)
        {
            return results;
        }

        foreach (var entry in enumerable)
        {
            var character = UnwrapNetValue(entry);
            if (!IsCharacterVisible(character))
            {
                continue;
            }

            var name = NormalizeOptionalName(GetStringMemberValue(character, "Name", "name"));
            if (name is not null)
            {
                results.Add(name);
            }
        }

        return results;
    }

    private static bool? DetermineInUpgradedHouse(object? player, object? currentLocation)
    {
        var locationName = GetLocationName(currentLocation);
        if (!string.Equals(locationName, "FarmHouse", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(locationName, "Cabin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(locationName, "FarmHouse", StringComparison.OrdinalIgnoreCase))
        {
            var playerUpgradeLevel = ExtractInt(player, "houseUpgradeLevel", "HouseUpgradeLevel", "HouseUpgradeValue");
            return playerUpgradeLevel is null ? null : playerUpgradeLevel.Value > 0;
        }

        var cabinUpgradeLevel = ExtractInt(
            currentLocation,
            "upgradeLevel",
            "UpgradeLevel",
            "houseUpgradeLevel",
            "HouseUpgradeLevel");
        return cabinUpgradeLevel is null ? null : cabinUpgradeLevel.Value > 0;
    }

    private static bool TryCollectRelationshipsFromEnumerable(
        object? source,
        HashSet<string> marriedNames,
        HashSet<string> engagedNames,
        HashSet<string> roommateNames)
    {
        if (source is null)
        {
            return false;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry pair in dictionary)
            {
                AddRelationshipEntry(pair.Key, pair.Value, marriedNames, engagedNames, roommateNames);
            }

            return true;
        }

        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var entry in enumerable)
        {
            var keyObject = GetMemberValue(entry, "Key");
            var valueObject = GetMemberValue(entry, "Value");
            AddRelationshipEntry(keyObject, valueObject, marriedNames, engagedNames, roommateNames);
        }

        return true;
    }

    private static void AddRelationshipEntry(
        object? rawKey,
        object? rawValue,
        HashSet<string> marriedNames,
        HashSet<string> engagedNames,
        HashSet<string> roommateNames)
    {
        var key = NormalizeOptionalName(ConvertToString(UnwrapNetValue(rawKey)));
        if (key is null)
        {
            return;
        }

        var friendship = UnwrapNetValue(rawValue);
        if (IsRoommateFriendship(friendship))
        {
            roommateNames.Add(key);
            return;
        }

        if (IsMarriedFriendship(friendship))
        {
            marriedNames.Add(key);
            return;
        }

        if (IsEngagedFriendship(friendship))
        {
            engagedNames.Add(key);
        }
    }

    private static bool TryCollectDatingFromEnumerable(object? source, HashSet<string> results)
    {
        if (source is null)
        {
            return false;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry pair in dictionary)
            {
                AddDatingEntry(pair.Key, pair.Value, results);
            }

            return true;
        }

        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var entry in enumerable)
        {
            var keyObject = GetMemberValue(entry, "Key");
            var valueObject = GetMemberValue(entry, "Value");
            AddDatingEntry(keyObject, valueObject, results);
        }

        return true;
    }

    private static void AddDatingEntry(object? rawKey, object? rawValue, HashSet<string> results)
    {
        var key = ConvertToString(UnwrapNetValue(rawKey));
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (IsDatingFriendship(UnwrapNetValue(rawValue)))
        {
            results.Add(key);
        }
    }

    private static bool IsDatingFriendship(object? friendship)
    {
        return HasFriendshipState(friendship, "Dating", "IsDating");
    }

    private static bool IsMarriedFriendship(object? friendship)
    {
        return HasFriendshipState(friendship, "Married", "IsMarried");
    }

    private static bool IsEngagedFriendship(object? friendship)
    {
        return HasFriendshipState(friendship, "Engaged", "IsEngaged");
    }

    private static bool IsRoommateFriendship(object? friendship)
    {
        return HasFriendshipState(friendship, "Roommate", "IsRoommate", "RoommateMarriage");
    }

    private static bool HasFriendshipState(object? friendship, string expectedStatus, params string[] flagNames)
    {
        if (friendship is null)
        {
            return false;
        }

        var directFlag = GetBoolMemberValue(friendship, flagNames);
        if (directFlag.HasValue)
        {
            return directFlag.Value;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var flagName in flagNames)
        {
            var method = friendship.GetType().GetMethod(flagName, flags);
            if (method is null || method.ReturnType != typeof(bool))
            {
                continue;
            }

            try
            {
                return (bool)method.Invoke(friendship, null)!;
            }
            catch
            {
                // Fall through to status probing below.
            }
        }

        var status = ConvertToString(UnwrapNetValue(GetMemberValue(friendship, "Status") ?? GetMemberValue(friendship, "status")));
        return string.Equals(status, expectedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCollectFriendshipFromEnumerable(object? source, Dictionary<string, int> results)
    {
        if (source is null)
        {
            return false;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry pair in dictionary)
            {
                AddFriendshipEntry(pair.Key, pair.Value, results);
            }

            return true;
        }

        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var entry in enumerable)
        {
            var keyObject = GetMemberValue(entry, "Key");
            var valueObject = GetMemberValue(entry, "Value");
            AddFriendshipEntry(keyObject, valueObject, results);
        }

        return true;
    }

    private static void AddFriendshipEntry(object? rawKey, object? rawValue, Dictionary<string, int> results)
    {
        var key = ConvertToString(UnwrapNetValue(rawKey));
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var friendship = UnwrapNetValue(rawValue);
        var points = ExtractInt(friendship, "Points", "points");
        if (points is not null)
        {
            results[key] = points.Value;
        }
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

    private static bool? DetermineIsFestivalDay(Type game1Type)
    {
        if (TryGetStaticBool(game1Type, out var value, "isFestival", "IsFestival"))
        {
            return value;
        }

        return null;
    }

    private static string ResolveDayOfWeek(Type game1Type, string season, int dayOfMonth)
    {
        var date = GetStaticMemberValue(game1Type, "Date");
        if (date is not null)
        {
            var dow = GetMemberValue(date, "DayOfWeek");
            if (dow is not null)
            {
                var name = Enum.GetName(dow.GetType(), dow) ?? ConvertToString(dow);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }

        return ComputeDayOfWeek(season, dayOfMonth);
    }

    private sealed class FarmhouseSnapshot
    {
        public bool Known { get; init; }

        public int? Level { get; init; }
    }

    private static FarmhouseSnapshot CollectFarmhouseUpgrade(object? player, bool? inUpgradedHouse)
    {
        foreach (var prop in new[] { "houseUpgradeLevel", "HouseUpgradeLevel", "farmHouseUpgradeLevel", "FarmHouseUpgradeLevel" })
        {
            var member = GetMemberValue(player, prop);
            if (member is null)
            {
                continue;
            }

            var level = ExtractInt(member, "Value");
            if (level.HasValue)
            {
                return new FarmhouseSnapshot { Known = true, Level = level.Value };
            }
        }

        if (inUpgradedHouse == false)
        {
            return new FarmhouseSnapshot { Known = true, Level = 0 };
        }

        return new FarmhouseSnapshot { Known = false, Level = null };
    }

    private sealed class SpouseBedSnapshot
    {
        public bool Known { get; init; }

        public bool? HasBed { get; init; }
    }

    private static SpouseBedSnapshot CollectSpouseBed(RelationshipState relationships, bool? inUpgradedHouse)
    {
        var hasSpouse = !string.IsNullOrWhiteSpace(relationships.SpouseName)
            || !string.IsNullOrWhiteSpace(relationships.Spouse)
            || !string.IsNullOrWhiteSpace(relationships.MarriedTo)
            || relationships.Spouses is { Length: > 0 };

        if (!hasSpouse)
        {
            return new SpouseBedSnapshot { Known = true, HasBed = false };
        }

        if (inUpgradedHouse == true)
        {
            return new SpouseBedSnapshot { Known = true, HasBed = true };
        }

        if (inUpgradedHouse == false)
        {
            return new SpouseBedSnapshot { Known = true, HasBed = false };
        }

        return new SpouseBedSnapshot { Known = false, HasBed = null };
    }

    private static (bool Known, List<string> Events) CollectDayEvents(Type game1Type)
    {
        var results = new List<string>();

        if (TryGetStaticBool(game1Type, out var isWeddingDay, "weddingToday", "isWeddingDay", "IsWeddingDay"))
        {
            if (isWeddingDay)
            {
                results.Add("wedding");
            }

            return (true, results);
        }

        return (false, results);
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

    private static bool TryGetStaticBool(Type type, out bool value, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
        {
            var rawValue = GetStaticMemberValue(type, memberName);
            if (TryConvertToBool(rawValue, out value))
            {
                return true;
            }
        }

        value = false;
        return false;
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

    private static bool IsCharacterVisible(object? character)
    {
        if (character is null)
        {
            return false;
        }

        var invisibleFlag = GetBoolMemberValue(character, "IsInvisible", "isInvisible", "Invisible");
        if (invisibleFlag.HasValue)
        {
            return !invisibleFlag.Value;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var method = character.GetType().GetMethod("isInvisible", flags)
            ?? character.GetType().GetMethod("IsInvisible", flags);
        if (method is not null && method.ReturnType == typeof(bool))
        {
            try
            {
                return !(bool)method.Invoke(character, null)!;
            }
            catch
            {
                return true;
            }
        }

        return true;
    }

    private static bool? GetBoolMemberValue(object? instance, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
        {
            var value = GetMemberValue(instance, memberName);
            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            var unwrappedValue = UnwrapNetValue(value);
            if (unwrappedValue is bool unwrappedBoolean)
            {
                return unwrappedBoolean;
            }
        }

        return null;
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

    private static bool TryConvertToBool(object? value, out bool parsed)
    {
        if (value is bool boolValue)
        {
            parsed = boolValue;
            return true;
        }

        var unwrappedValue = UnwrapNetValue(value);
        if (unwrappedValue is bool unwrappedBool)
        {
            parsed = unwrappedBool;
            return true;
        }

        parsed = false;
        return false;
    }

    private static string? NormalizeOptionalName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed record RelationshipState(
        string? SpouseName,
        string? Spouse,
        string? MarriedTo,
        string[]? Spouses,
        string? EngagedTo,
        string? Roommate);
}
