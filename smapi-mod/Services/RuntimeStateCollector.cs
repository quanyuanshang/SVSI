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
        var year = GetStaticInt(game1Type, "year");
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
        var dialogueAnswers = CollectStringSet(
            player,
            "dialogueQuestionsAnswered",
            "dialogueQuestionsAnsweredThisSeason",
            "dialogueAnswers"
        );
        var inventorySnapshot = CollectInventoryItems(player);
        var familySnapshot = CollectFamilyState(player, relationships);
        var marriageSnapshot = CollectMarriageState(player, relationships, year, season, dayOfMonth);
        var activeQuestSnapshot = CollectActiveQuests(player);

        return new RuntimeGameState
        {
            Year = year,
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
            FamilyStateKnown = familySnapshot.Known,
            PregnantPlayers = familySnapshot.PregnantPlayers,
            HavingChildPlayers = familySnapshot.HavingChildPlayers,
            ChildrenCount = familySnapshot.ChildrenCount,
            ChildGenders = familySnapshot.ChildGenders,
            YearsMarriedKnown = marriageSnapshot.Known,
            YearsMarried = marriageSnapshot.YearsMarried,
            AnniversarySeason = marriageSnapshot.AnniversarySeason,
            AnniversaryDay = marriageSnapshot.AnniversaryDay,
            HasItemKnown = inventorySnapshot.Known,
            InventoryItemIds = inventorySnapshot.ItemIds,
            SpouseBedKnown = spouseBedSnapshot.Known,
            HasSpouseBed = spouseBedSnapshot.HasBed,
            SeenEvents = CollectStringSet(player, "eventsSeen"),
            Mail = CollectStringSet(player, "mailReceived"),
            DialogueAnswers = dialogueAnswers,
            DialogueAnswersKnown = true,
            DialogueAnswerIds = new HashSet<string>(dialogueAnswers, StringComparer.Ordinal),
            ActiveDialogueEventsKnown = activeDialogueSnapshot.Known,
            ActiveDialogueEvents = activeDialogueSnapshot.Events,
            ActiveQuestsKnown = activeQuestSnapshot.Known,
            ActiveQuestIds = activeQuestSnapshot.QuestIds
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

    private sealed class ActiveQuestSnapshot
    {
        public bool Known { get; init; }

        public HashSet<string> QuestIds { get; init; } = new(StringComparer.Ordinal);
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

        if (TryInvokeStaticBool(game1Type, out value, "isFestival", "IsFestival"))
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

    private sealed class InventorySnapshot
    {
        public bool Known { get; init; }

        public HashSet<string> ItemIds { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class FamilyStateSnapshot
    {
        public bool Known { get; init; }

        public string[] PregnantPlayers { get; init; } = Array.Empty<string>();

        public string[] HavingChildPlayers { get; init; } = Array.Empty<string>();

        public int? ChildrenCount { get; init; }

        public string[] ChildGenders { get; init; } = Array.Empty<string>();
    }

    private sealed class MarriageSnapshot
    {
        public bool Known { get; init; }

        public int? YearsMarried { get; init; }

        public string? AnniversarySeason { get; init; }

        public int? AnniversaryDay { get; init; }
    }

    private static InventorySnapshot CollectInventoryItems(object? player)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        var items = GetMemberValue(player, "Items") ?? GetMemberValue(player, "items");
        if (items is not IEnumerable enumerable)
        {
            return new InventorySnapshot { Known = false, ItemIds = results };
        }

        foreach (var entry in enumerable)
        {
            var item = UnwrapNetValue(entry);
            if (item is null)
            {
                continue;
            }

            foreach (var value in new[]
            {
                ConvertToString(GetMemberValue(item, "QualifiedItemId")),
                ConvertToString(GetMemberValue(item, "ItemId")),
                ConvertToString(GetMemberValue(item, "ParentSheetIndex")),
                ConvertToString(GetMemberValue(item, "Name")),
                ConvertToString(GetMemberValue(item, "DisplayName"))
            })
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value.Trim());
                }
            }
        }

        return new InventorySnapshot { Known = true, ItemIds = results };
    }

    private static FamilyStateSnapshot CollectFamilyState(object? player, RelationshipState relationships)
    {
        var childGenders = new List<string>();
        var childrenCount = TryCollectChildren(player, childGenders, out var hasChildrenData);
        var daysUntilBirthing = ExtractInt(player, "DaysUntilBirthing", "daysUntilBirthing");
        var nextBirthingDate = GetMemberValue(player, "NextBirthingDate") ?? GetMemberValue(player, "nextBirthingDate");
        var canGetPregnant = GetBoolMemberValue(player, "CanGetPregnant", "canGetPregnant", "CanHavePregnancy", "canHavePregnancy");
        var hasPendingChild = (daysUntilBirthing is int birthingDays && birthingDays >= 0)
            || nextBirthingDate is not null;

        var hasFamilyData = hasChildrenData || daysUntilBirthing is not null || nextBirthingDate is not null || canGetPregnant.HasValue;
        if (!hasFamilyData)
        {
            return new FamilyStateSnapshot { Known = false };
        }

        var pregnantPlayers = new HashSet<string>(StringComparer.Ordinal);
        var havingChildPlayers = new HashSet<string>(StringComparer.Ordinal);
        var playerName = NormalizeOptionalName(GetStringMemberValue(player, "Name", "name"));

        if (hasPendingChild && playerName is not null)
        {
            havingChildPlayers.Add(playerName);
            if (canGetPregnant == true)
            {
                pregnantPlayers.Add(playerName);
            }
        }

        return new FamilyStateSnapshot
        {
            Known = true,
            PregnantPlayers = pregnantPlayers.OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            HavingChildPlayers = havingChildPlayers.OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            ChildrenCount = childrenCount,
            ChildGenders = childGenders.ToArray()
        };
    }

    private static MarriageSnapshot CollectMarriageState(
        object? player,
        RelationshipState relationships,
        int currentYear,
        string currentSeason,
        int currentDayOfMonth)
    {
        var daysMarried = ExtractInt(player, "daysMarried", "DaysMarried");
        var weddingDate = GetMemberValue(player, "weddingDate") ?? GetMemberValue(player, "WeddingDate");
        if (daysMarried is null && weddingDate is null && TryCollectMarriageStateFromFriendships(player, currentYear, currentSeason, currentDayOfMonth, out var friendshipMarriage))
        {
            return friendshipMarriage;
        }

        if (daysMarried is null && weddingDate is null)
        {
            if (relationships.SpouseName is null
                && relationships.Spouse is null
                && relationships.MarriedTo is null
                && (relationships.Spouses is null || relationships.Spouses.Length == 0))
            {
                return new MarriageSnapshot { Known = true, YearsMarried = 0 };
            }

            return new MarriageSnapshot { Known = false };
        }

        return new MarriageSnapshot
        {
            Known = true,
            YearsMarried = daysMarried.HasValue ? Math.Max(0, daysMarried.Value / 112) : null,
            AnniversarySeason = GetStringMemberValue(weddingDate, "Season", "season"),
            AnniversaryDay = ExtractInt(weddingDate, "Day", "day", "DayOfMonth", "dayOfMonth")
        };
    }

    private static bool TryCollectMarriageStateFromFriendships(
        object? player,
        int currentYear,
        string currentSeason,
        int currentDayOfMonth,
        out MarriageSnapshot snapshot)
    {
        snapshot = new MarriageSnapshot { Known = false };
        var friendshipData = GetMemberValue(player, "friendshipData");
        if (friendshipData is null)
        {
            return false;
        }

        var enumerationSources = new[]
        {
            GetMemberValue(friendshipData, "FieldDict"),
            GetMemberValue(friendshipData, "Pairs"),
            friendshipData
        };

        foreach (var source in enumerationSources)
        {
            if (TryCollectMarriageStateFromFriendshipEnumerable(source, currentYear, currentSeason, currentDayOfMonth, out snapshot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCollectMarriageStateFromFriendshipEnumerable(
        object? source,
        int currentYear,
        string currentSeason,
        int currentDayOfMonth,
        out MarriageSnapshot snapshot)
    {
        snapshot = new MarriageSnapshot { Known = false };
        if (source is null)
        {
            return false;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry pair in dictionary)
            {
                if (TryCreateMarriageSnapshotFromFriendship(pair.Value, currentYear, currentSeason, currentDayOfMonth, out snapshot))
                {
                    return true;
                }
            }

            return false;
        }

        if (source is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var entry in enumerable)
        {
            if (TryCreateMarriageSnapshotFromFriendship(GetMemberValue(entry, "Value"), currentYear, currentSeason, currentDayOfMonth, out snapshot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateMarriageSnapshotFromFriendship(
        object? rawFriendship,
        int currentYear,
        string currentSeason,
        int currentDayOfMonth,
        out MarriageSnapshot snapshot)
    {
        snapshot = new MarriageSnapshot { Known = false };
        var friendship = UnwrapNetValue(rawFriendship);
        if (!IsMarriedFriendship(friendship))
        {
            return false;
        }

        var daysMarried = ExtractInt(friendship, "daysMarried", "DaysMarried", "daysUntilMarriage", "DaysUntilMarriage");
        var weddingDate = GetMemberValue(friendship, "weddingDate") ?? GetMemberValue(friendship, "WeddingDate");
        var anniversarySeason = GetStringMemberValue(weddingDate, "Season", "season");
        var anniversaryDay = ExtractInt(weddingDate, "Day", "day", "DayOfMonth", "dayOfMonth");
        if (daysMarried is null && weddingDate is not null)
        {
            daysMarried = EstimateDaysSinceWedding(weddingDate, currentYear, currentSeason, currentDayOfMonth);
        }

        if (daysMarried is null && weddingDate is null)
        {
            return false;
        }

        snapshot = new MarriageSnapshot
        {
            Known = true,
            YearsMarried = daysMarried.HasValue ? Math.Max(0, daysMarried.Value / 112) : null,
            AnniversarySeason = anniversarySeason,
            AnniversaryDay = anniversaryDay
        };
        return true;
    }

    private static int? EstimateDaysSinceWedding(object weddingDate, int currentYear, string currentSeason, int currentDayOfMonth)
    {
        var weddingYear = ExtractInt(weddingDate, "Year", "year");
        var weddingSeason = GetStringMemberValue(weddingDate, "Season", "season");
        var weddingDay = ExtractInt(weddingDate, "Day", "day", "DayOfMonth", "dayOfMonth");
        if (weddingYear is null || string.IsNullOrWhiteSpace(weddingSeason) || weddingDay is null)
        {
            return null;
        }

        var currentAbsoluteDay = ToAbsoluteStardewDay(currentYear, currentSeason, currentDayOfMonth);
        var weddingAbsoluteDay = ToAbsoluteStardewDay(weddingYear.Value, weddingSeason, weddingDay.Value);
        if (currentAbsoluteDay is null || weddingAbsoluteDay is null)
        {
            return null;
        }

        return Math.Max(0, currentAbsoluteDay.Value - weddingAbsoluteDay.Value);
    }

    private static int? ToAbsoluteStardewDay(int year, string season, int dayOfMonth)
    {
        var seasonIndex = season.Trim().ToLowerInvariant() switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => (int?)null
        };
        if (seasonIndex is null)
        {
            return null;
        }

        return (Math.Max(1, year) - 1) * 112 + seasonIndex.Value * 28 + Math.Max(1, dayOfMonth);
    }

    private static ActiveQuestSnapshot CollectActiveQuests(object? player)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        var questLog = GetMemberValue(player, "questLog") ?? GetMemberValue(player, "QuestLog");
        if (questLog is not IEnumerable enumerable)
        {
            return new ActiveQuestSnapshot { Known = false, QuestIds = results };
        }

        foreach (var entry in enumerable)
        {
            var quest = UnwrapNetValue(entry);
            if (quest is null)
            {
                continue;
            }

            foreach (var value in new[]
            {
                ConvertToString(GetMemberValue(quest, "id")),
                ConvertToString(GetMemberValue(quest, "Id")),
                ConvertToString(GetMemberValue(quest, "questId")),
                ConvertToString(GetMemberValue(quest, "QuestId")),
                ConvertToString(GetMemberValue(quest, "questID")),
                ConvertToString(GetMemberValue(quest, "QuestID"))
            })
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value.Trim());
                }
            }
        }

        return new ActiveQuestSnapshot { Known = true, QuestIds = results };
    }

    private static int? TryCollectChildren(object? player, ICollection<string> childGenders, out bool known)
    {
        known = false;
        var children = GetMemberValue(player, "Children") ?? GetMemberValue(player, "children");
        if (children is IEnumerable enumerable)
        {
            var count = 0;
            foreach (var entry in enumerable)
            {
                var child = UnwrapNetValue(entry);
                if (child is null)
                {
                    continue;
                }

                count++;
                var gender = GetChildGender(child);
                if (!string.IsNullOrWhiteSpace(gender))
                {
                    childGenders.Add(gender);
                }
            }

            known = true;
            return count;
        }

        var countValue = ExtractInt(player, "childrenCount", "ChildrenCount", "numberOfChildren", "NumberOfChildren");
        if (countValue is not null)
        {
            known = true;
            return countValue;
        }

        var getChildrenCount = InvokeIntMethod(player, "getChildrenCount", "getNumberOfChildren");
        if (getChildrenCount is not null)
        {
            known = true;
            return getChildrenCount;
        }

        return null;
    }

    private static string? GetChildGender(object? child)
    {
        if (child is null)
        {
            return null;
        }

        var raw = ConvertToString(
            UnwrapNetValue(
                GetMemberValue(child, "Gender")
                ?? GetMemberValue(child, "gender")
                ?? GetMemberValue(child, "Age")
                ?? GetMemberValue(child, "age")));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim();
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

    private static bool TryInvokeStaticBool(Type type, out bool value, params string[] methodNames)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        foreach (var methodName in methodNames)
        {
            var method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            if (method?.ReturnType != typeof(bool))
            {
                continue;
            }

            var result = method.Invoke(null, null);
            if (result is bool boolResult)
            {
                value = boolResult;
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

    private static int? InvokeIntMethod(object? instance, params string[] methodNames)
    {
        if (instance is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var methodName in methodNames)
        {
            var method = instance.GetType().GetMethod(
                methodName,
                flags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                continue;
            }

            try
            {
                var result = method.Invoke(instance, null);
                if (result is int intValue)
                {
                    return intValue;
                }

                var parsed = ExtractInt(result);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch
            {
                // Ignore reflection failures and keep probing fallbacks.
            }
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
