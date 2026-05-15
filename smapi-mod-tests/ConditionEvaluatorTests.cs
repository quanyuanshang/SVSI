using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class ConditionEvaluatorTests
{
    public static void RunAll()
    {
        Evaluate_Season_TrueAndFalse();
        Evaluate_FestivalDay_TrueAndFalse();
        Evaluate_FestivalDay_MissingRuntimeDefaultsFalse();
        Evaluate_Time_TrueAndFalse();
        Evaluate_Year_TrueAndFalse();
        Evaluate_Spouse_TrueAndFalse();
        Evaluate_IsHost_DefaultsTrue();
        Evaluate_Friendship_TrueAndFalse();
        Evaluate_Friendship_MinFriendshipPlaceholder_IsExternalTokenMissing();
        Evaluate_Dating_TrueAndFalse();
        Evaluate_NpcVisibleHere_TrueAndFalse();
        Evaluate_InUpgradedHouse_TrueAndFalse();
        Evaluate_SpouseBed_BAlias_RuntimeStates();
        Evaluate_SawEvent_TrueAndFalse();
        Evaluate_Mail_TrueAndFalse();
        Evaluate_ChoseDialogueAnswers_TrueAndFalse();
        Evaluate_AllOf();
        Evaluate_AnyOf();
        Evaluate_Not();
        Evaluate_Unknown();
        Evaluate_DayOfWeekNegatedAbbreviations_OnSunday_IsUnsatisfied();
        Evaluate_DayOfWeekNegatedMonThroughThu_OnSunday_Passes();
        Evaluate_NotGameStateQuerySeasonDay_OnSummer7_Passes();
        Evaluate_GameStateQuerySeasonDay_OnSummer28_Passes();
        Evaluate_GameStateQuerySeasonDay_OnSummer7_Fails();
        Evaluate_GameStateQueryNpcRelationship_Married_Passes();
        Evaluate_GameStateQueryNpcRelationship_DatingOnly_Fails();
        Evaluate_NotGameStateQueryNpcRelationship_DatingOnly_Passes();
    }

    private static void Evaluate_Season_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("Season", "Season fall", "fall"), state);
        AssertEqual(true, matched.Passed, "Season true case mismatch.");
        AssertEqual(false, matched.HasUnknown, "Season true case should not be unknown.");
        AssertEqual(1, matched.AtomResults.Count, "Season true case should produce one atom result.");
        AssertEqual(true, matched.AtomResults[0].IsContextSensitive, "Season should be context-sensitive.");
        AssertEqual(false, matched.AtomResults[0].IsProgressionSensitive, "Season should not be progression-sensitive.");

        var failed = evaluator.Evaluate(CreateAtom("Season", "Season spring", "spring"), state);
        AssertEqual(false, failed.Passed, "Season false case mismatch.");
        AssertContains(failed.Reason, "Season failed", "Season false reason mismatch.");
    }

    private static void Evaluate_Time_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("Time", "t 1800 2000", "1800", "2000"), state);
        AssertEqual(true, matched.Passed, "Time true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("Time", "t 600 1200", "600", "1200"), state);
        AssertEqual(false, failed.Passed, "Time false case mismatch.");
        AssertContains(failed.Reason, "outside", "Time false reason mismatch.");
    }

    private static void Evaluate_FestivalDay_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();

        var notFestival = evaluator.Evaluate(CreateAtom("FestivalDay", "F"), CreateBaseState(isFestivalDay: false));
        AssertEqual(false, notFestival.Passed, "FestivalDay should fail when today is not a festival.");

        var festival = evaluator.Evaluate(CreateAtom("FestivalDay", "F"), CreateBaseState(isFestivalDay: true));
        AssertEqual(true, festival.Passed, "FestivalDay should pass when today is a festival.");
        AssertEqual(true, festival.AtomResults[0].IsContextSensitive, "FestivalDay should be context-sensitive.");
    }

    private static void Evaluate_FestivalDay_MissingRuntimeDefaultsFalse()
    {
        var evaluator = new ConditionEvaluator();
        var result = evaluator.Evaluate(CreateAtom("FestivalDay", "F"), CreateBaseState(isFestivalDay: null));
        AssertEqual(false, result.Passed, "Missing festival runtime should be treated as not currently festival for legacy F.");
        AssertEqual(false, result.HasUnknown, "Missing festival runtime should not make legacy F unknown.");
    }

    private static void Evaluate_Year_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        AssertEqual(true, evaluator.Evaluate(CreateAtom("Year", "y 1", "1"), state).Passed, "Year true case mismatch.");
        AssertEqual(false, evaluator.Evaluate(CreateAtom("Year", "y 2", "2"), state).Passed, "Year false case mismatch.");
    }

    private static void Evaluate_Spouse_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState(spouse: "Wizard");
        AssertEqual(true, evaluator.Evaluate(CreateAtom("Spouse", "O Wizard", "Wizard"), state).Passed, "Spouse true case mismatch.");
        AssertEqual(false, evaluator.Evaluate(CreateAtom("Spouse", "O Sam", "Sam"), state).Passed, "Spouse false case mismatch.");
    }

    private static void Evaluate_IsHost_DefaultsTrue()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        AssertEqual(true, evaluator.Evaluate(CreateAtom("IsHost", "H"), state).Passed, "Offline runtime should treat current player as host.");
    }

    private static void Evaluate_Friendship_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("Friendship", "f Shane 2000", "Shane", "2000"), state);
        AssertEqual(true, matched.Passed, "Friendship true case mismatch.");
        AssertEqual(true, matched.AtomResults[0].IsProgressionSensitive, "Friendship should be progression-sensitive.");

        var failed = evaluator.Evaluate(CreateAtom("Friendship", "f Shane 3000", "Shane", "3000"), state);
        AssertEqual(false, failed.Passed, "Friendship false case mismatch.");
        AssertContains(failed.Reason, "requires 3000", "Friendship false reason mismatch.");
    }

    private static void Evaluate_Dating_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("Dating", "D Shane", "Shane"), state);
        AssertEqual(true, matched.Passed, "Dating true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("Dating", "D Sebastian", "Sebastian"), state);
        AssertEqual(false, failed.Passed, "Dating false case mismatch.");
    }

    private static void Evaluate_NpcVisibleHere_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("NpcVisibleHere", "p Shane", "Shane"), state);
        AssertEqual(true, matched.Passed, "NpcVisibleHere true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("NpcVisibleHere", "p Sebastian", "Sebastian"), state);
        AssertEqual(false, failed.Passed, "NpcVisibleHere false case mismatch.");
    }

    private static void Evaluate_InUpgradedHouse_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var upgraded = CreateBaseState(inUpgradedHouse: true);
        var notUpgraded = CreateBaseState(inUpgradedHouse: false);

        var matched = evaluator.Evaluate(CreateAtom("InUpgradedHouse", "L"), upgraded);
        AssertEqual(true, matched.Passed, "InUpgradedHouse true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("InUpgradedHouse", "L"), notUpgraded);
        AssertEqual(false, failed.Passed, "InUpgradedHouse false case mismatch.");
    }

    private static void Evaluate_SpouseBed_BAlias_RuntimeStates()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "B" });
        var evaluator = new ConditionEvaluator();

        var unknownState = CreateBaseState();
        unknownState = new RuntimeGameState
        {
            Year = unknownState.Year,
            Season = unknownState.Season,
            DayOfMonth = unknownState.DayOfMonth,
            DayOfWeek = unknownState.DayOfWeek,
            Time = unknownState.Time,
            Weather = unknownState.Weather,
            IsFestivalDay = unknownState.IsFestivalDay,
            CurrentLocation = unknownState.CurrentLocation,
            PlayerName = unknownState.PlayerName,
            FriendshipPoints = unknownState.FriendshipPoints,
            DatingNpcNames = unknownState.DatingNpcNames,
            VisibleNpcNamesHere = unknownState.VisibleNpcNamesHere,
            InUpgradedHouse = unknownState.InUpgradedHouse,
            SpouseBedKnown = false,
            HasSpouseBed = null,
            SeenEvents = unknownState.SeenEvents,
            Mail = unknownState.Mail,
            DialogueAnswers = unknownState.DialogueAnswers
        };

        var unknownResult = evaluator.Evaluate(parsed.ConditionAst, unknownState);
        AssertEqual(null, unknownResult.Passed, "SpouseBed without runtime certainty should be unknown.");
        AssertEqual("SpouseBed", unknownResult.AtomResults[0].AtomType, "B should parse as SpouseBed atom.");
        AssertEqual("runtimeMissing", unknownResult.AtomResults[0].UnknownKind, "SpouseBed unknown kind mismatch.");

        var knownState = CreateBaseState(spouse: "Sam");
        knownState = new RuntimeGameState
        {
            Year = knownState.Year,
            Season = knownState.Season,
            DayOfMonth = knownState.DayOfMonth,
            DayOfWeek = knownState.DayOfWeek,
            Time = knownState.Time,
            Weather = knownState.Weather,
            IsFestivalDay = knownState.IsFestivalDay,
            CurrentLocation = knownState.CurrentLocation,
            PlayerName = knownState.PlayerName,
            SpouseName = knownState.SpouseName,
            Spouse = knownState.Spouse,
            FriendshipPoints = knownState.FriendshipPoints,
            DatingNpcNames = knownState.DatingNpcNames,
            VisibleNpcNamesHere = knownState.VisibleNpcNamesHere,
            InUpgradedHouse = true,
            SpouseBedKnown = true,
            HasSpouseBed = true,
            SeenEvents = knownState.SeenEvents,
            Mail = knownState.Mail,
            DialogueAnswers = knownState.DialogueAnswers
        };

        var passResult = evaluator.Evaluate(parsed.ConditionAst, knownState);
        AssertEqual(true, passResult.Passed, "SpouseBed should pass when spouse and upgraded house are known.");
        AssertEqual(false, passResult.HasUnknown, "SpouseBed with known snapshot should not be unknown.");
    }

    private static void Evaluate_Friendship_MinFriendshipPlaceholder_IsExternalTokenMissing()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        var result = evaluator.Evaluate(
            CreateAtom("Friendship", "f Alex {{MinFriendship}}", "Alex", "{{MinFriendship}}"),
            state);

        AssertEqual(null, result.Passed, "Unresolved MinFriendship token should be unknown.");
        AssertEqual(true, result.HasUnknown, "MinFriendship should surface as unknown evaluation.");
        AssertEqual("ExternalTokenMissing", result.AtomResults[0].AtomType, "MinFriendship should classify as ExternalTokenMissing.");
        AssertEqual("externalTokenMissing", result.AtomResults[0].UnknownKind, "MinFriendship unknown kind mismatch.");
        AssertContains(result.AtomResults[0].ReasonZh!, "MinFriendship", "MinFriendship reason should mention the token.");
    }

    private static void Evaluate_SawEvent_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("SawEvent", "e 100001", "100001"), state);
        AssertEqual(true, matched.Passed, "SawEvent true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("SawEvent", "e 999999", "999999"), state);
        AssertEqual(false, failed.Passed, "SawEvent false case mismatch.");
    }

    private static void Evaluate_Mail_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("LocalMail", "n someMail", "someMail"), state);
        AssertEqual(true, matched.Passed, "Mail true case mismatch.");
        AssertEqual(true, matched.AtomResults[0].IsProgressionSensitive, "Mail should be progression-sensitive.");

        var failed = evaluator.Evaluate(CreateAtom("HostMail", "HostMail missingMail", "missingMail"), state);
        AssertEqual(false, failed.Passed, "Mail false case mismatch.");
    }

    private static void Evaluate_ChoseDialogueAnswers_TrueAndFalse()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var matched = evaluator.Evaluate(CreateAtom("ChoseDialogueAnswers", "q ShaneAnswerA", "ShaneAnswerA"), state);
        AssertEqual(true, matched.Passed, "Dialogue answer true case mismatch.");

        var failed = evaluator.Evaluate(CreateAtom("ChoseDialogueAnswers", "q MissingAnswer", "MissingAnswer"), state);
        AssertEqual(false, failed.Passed, "Dialogue answer false case mismatch.");
    }

    private static void Evaluate_AllOf()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var result = evaluator.Evaluate(
            new ConditionAstNode
            {
                Type = "AllOf",
                Children =
                {
                    CreateAtom("Season", "Season fall", "fall"),
                    CreateAtom("Weather", "Weather sunny", "sunny")
                }
            },
            state
        );

        AssertEqual(true, result.Passed, "AllOf case mismatch.");
        AssertEqual(2, result.AtomResults.Count, "AllOf should flatten atom results.");
    }

    private static void Evaluate_AnyOf()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var result = evaluator.Evaluate(
            new ConditionAstNode
            {
                Type = "AnyOf",
                Children =
                {
                    CreateAtom("Season", "Season spring", "spring"),
                    CreateAtom("Weather", "Weather sunny", "sunny")
                }
            },
            state
        );

        AssertEqual(true, result.Passed, "AnyOf case mismatch.");
    }

    private static void Evaluate_Not()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var result = evaluator.Evaluate(
            new ConditionAstNode
            {
                Type = "Not",
                Operand = CreateAtom("Season", "Season spring", "spring")
            },
            state
        );

        AssertEqual(true, result.Passed, "Not case mismatch.");
    }

    private static void Evaluate_DayOfWeekNegatedAbbreviations_OnSunday_IsUnsatisfied()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "d Mon Tue Wed Thu Sat Sun" });
        var evaluator = new ConditionEvaluator();
        var baseState = CreateBaseState();
        var state = new RuntimeGameState
        {
            Year = baseState.Year,
            Season = baseState.Season,
            DayOfMonth = baseState.DayOfMonth,
            DayOfWeek = "Sunday",
            Time = baseState.Time,
            Weather = baseState.Weather,
            IsFestivalDay = baseState.IsFestivalDay,
            CurrentLocation = baseState.CurrentLocation,
            PlayerName = baseState.PlayerName,
            InstalledModIds = baseState.InstalledModIds,
            FriendshipPoints = baseState.FriendshipPoints,
            DatingNpcNames = baseState.DatingNpcNames,
            VisibleNpcNamesHere = baseState.VisibleNpcNamesHere,
            InUpgradedHouse = baseState.InUpgradedHouse,
            SeenEvents = baseState.SeenEvents,
            Mail = baseState.Mail,
            DialogueAnswers = baseState.DialogueAnswers,
            DayEvents = baseState.DayEvents
        };

        var result = evaluator.Evaluate(parsed.ConditionAst, state);

        AssertEqual(false, result.Passed, "Sunday is in the excluded list, so the negated day condition should fail.");
        AssertEqual(false, result.HasUnknown, "Abbreviated weekdays should be known, not unknown.");
    }

    private static void Evaluate_DayOfWeekNegatedMonThroughThu_OnSunday_Passes()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "d Mon Tue Wed Thu" });
        var evaluator = new ConditionEvaluator();
        var baseState = CreateBaseState();
        var state = new RuntimeGameState
        {
            Year = baseState.Year,
            Season = baseState.Season,
            DayOfMonth = baseState.DayOfMonth,
            DayOfWeek = "Sunday",
            Time = baseState.Time,
            Weather = baseState.Weather,
            IsFestivalDay = baseState.IsFestivalDay,
            CurrentLocation = baseState.CurrentLocation,
            PlayerName = baseState.PlayerName,
            FriendshipPoints = baseState.FriendshipPoints,
            DatingNpcNames = baseState.DatingNpcNames,
            VisibleNpcNamesHere = baseState.VisibleNpcNamesHere,
            InUpgradedHouse = baseState.InUpgradedHouse,
            SeenEvents = baseState.SeenEvents,
            Mail = baseState.Mail,
            DialogueAnswers = baseState.DialogueAnswers
        };

        var result = evaluator.Evaluate(parsed.ConditionAst, state);

        AssertEqual(true, result.Passed, "Sunday is not Mon–Thu, so the negated weekday list should pass.");
        AssertEqual(false, result.HasUnknown, "Weekday abbreviations should evaluate without unknown.");
    }

    private static void Evaluate_NotGameStateQuerySeasonDay_OnSummer7_Passes()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "!G SEASON_DAY summer 28" });
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        state = new RuntimeGameState
        {
            Year = state.Year,
            Season = "summer",
            DayOfMonth = 7,
            DayOfWeek = state.DayOfWeek,
            Time = state.Time,
            Weather = state.Weather,
            IsFestivalDay = state.IsFestivalDay,
            CurrentLocation = state.CurrentLocation,
            PlayerName = state.PlayerName,
            FriendshipPoints = state.FriendshipPoints,
            DatingNpcNames = state.DatingNpcNames,
            VisibleNpcNamesHere = state.VisibleNpcNamesHere,
            InUpgradedHouse = state.InUpgradedHouse,
            SeenEvents = state.SeenEvents,
            Mail = state.Mail,
            DialogueAnswers = state.DialogueAnswers,
            DayEventsKnown = state.DayEventsKnown,
            DayEvents = state.DayEvents
        };

        var result = evaluator.Evaluate(parsed.ConditionAst, state);
        AssertEqual(true, result.Passed, "!G SEASON_DAY summer 28 should pass on summer day 7.");
        AssertEqual(false, result.HasUnknown, "!G SEASON_DAY should be known.");
    }

    private static void Evaluate_GameStateQuerySeasonDay_OnSummer28_Passes()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "G SEASON_DAY summer 28" });
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        state = new RuntimeGameState
        {
            Year = state.Year,
            Season = "summer",
            DayOfMonth = 28,
            DayOfWeek = state.DayOfWeek,
            Time = state.Time,
            Weather = state.Weather,
            IsFestivalDay = state.IsFestivalDay,
            CurrentLocation = state.CurrentLocation,
            PlayerName = state.PlayerName,
            FriendshipPoints = state.FriendshipPoints,
            DatingNpcNames = state.DatingNpcNames,
            VisibleNpcNamesHere = state.VisibleNpcNamesHere,
            InUpgradedHouse = state.InUpgradedHouse,
            SeenEvents = state.SeenEvents,
            Mail = state.Mail,
            DialogueAnswers = state.DialogueAnswers,
            DayEventsKnown = state.DayEventsKnown,
            DayEvents = state.DayEvents
        };

        var result = evaluator.Evaluate(parsed.ConditionAst, state);
        AssertEqual(true, result.Passed, "G SEASON_DAY summer 28 should pass on summer day 28.");
    }

    private static void Evaluate_GameStateQuerySeasonDay_OnSummer7_Fails()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "G SEASON_DAY summer 28" });
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();
        state = new RuntimeGameState
        {
            Year = state.Year,
            Season = "summer",
            DayOfMonth = 7,
            DayOfWeek = state.DayOfWeek,
            Time = state.Time,
            Weather = state.Weather,
            IsFestivalDay = state.IsFestivalDay,
            CurrentLocation = state.CurrentLocation,
            PlayerName = state.PlayerName,
            FriendshipPoints = state.FriendshipPoints,
            DatingNpcNames = state.DatingNpcNames,
            VisibleNpcNamesHere = state.VisibleNpcNamesHere,
            InUpgradedHouse = state.InUpgradedHouse,
            SeenEvents = state.SeenEvents,
            Mail = state.Mail,
            DialogueAnswers = state.DialogueAnswers,
            DayEventsKnown = state.DayEventsKnown,
            DayEvents = state.DayEvents
        };

        var result = evaluator.Evaluate(parsed.ConditionAst, state);
        AssertEqual(false, result.Passed, "G SEASON_DAY summer 28 should fail on summer day 7.");
    }

    private static void Evaluate_GameStateQueryNpcRelationship_Married_Passes()
    {
        EvaluateRelationshipPasses("G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married", CreateSebastianMarriedState());
    }

    private static void Evaluate_GameStateQueryNpcRelationship_DatingOnly_Fails()
    {
        EvaluateRelationshipFails("G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married", CreateSebastianDatingState());
    }

    private static void Evaluate_NotGameStateQueryNpcRelationship_DatingOnly_Passes()
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { "!G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married" });
        var evaluator = new ConditionEvaluator();
        var result = evaluator.Evaluate(parsed.ConditionAst, CreateSebastianDatingState());
        AssertEqual(true, result.Passed, "Negated relationship should pass when only dating.");
    }

    private static void EvaluateRelationshipPasses(string raw, RuntimeGameState state)
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { raw });
        var evaluator = new ConditionEvaluator();
        var result = evaluator.Evaluate(parsed.ConditionAst, state);
        AssertEqual(true, result.Passed, $"Relationship should pass for {raw}.");
        AssertEqual(false, result.HasUnknown, "Relationship should be known.");
    }

    private static void EvaluateRelationshipFails(string raw, RuntimeGameState state)
    {
        var parser = new EventPreconditionParser();
        var parsed = parser.Parse(new[] { raw });
        var evaluator = new ConditionEvaluator();
        var result = evaluator.Evaluate(parsed.ConditionAst, state);
        AssertEqual(false, result.Passed, $"Relationship should fail for {raw}.");
    }

    private static RuntimeGameState CreateSebastianMarriedState()
    {
        var state = CreateBaseState();
        return new RuntimeGameState
        {
            Year = state.Year,
            Season = state.Season,
            DayOfMonth = state.DayOfMonth,
            DayOfWeek = state.DayOfWeek,
            Time = state.Time,
            Weather = state.Weather,
            IsFestivalDay = state.IsFestivalDay,
            CurrentLocation = state.CurrentLocation,
            PlayerName = state.PlayerName,
            FriendshipPoints = state.FriendshipPoints,
            DatingNpcNames = state.DatingNpcNames,
            VisibleNpcNamesHere = state.VisibleNpcNamesHere,
            InUpgradedHouse = state.InUpgradedHouse,
            SeenEvents = state.SeenEvents,
            Mail = state.Mail,
            DialogueAnswers = state.DialogueAnswers,
            DayEventsKnown = state.DayEventsKnown,
            DayEvents = state.DayEvents,
            SpouseName = "Sebastian",
            Spouse = "Sebastian",
            MarriedTo = "Sebastian",
            Spouses = new[] { "Sebastian" }
        };
    }

    private static RuntimeGameState CreateSebastianDatingState()
    {
        var state = CreateBaseState();
        return new RuntimeGameState
        {
            Year = state.Year,
            Season = state.Season,
            DayOfMonth = state.DayOfMonth,
            DayOfWeek = state.DayOfWeek,
            Time = state.Time,
            Weather = state.Weather,
            IsFestivalDay = state.IsFestivalDay,
            CurrentLocation = state.CurrentLocation,
            PlayerName = state.PlayerName,
            FriendshipPoints = state.FriendshipPoints,
            DatingNpcNames = new HashSet<string>(new[] { "Sebastian" }, StringComparer.Ordinal),
            VisibleNpcNamesHere = state.VisibleNpcNamesHere,
            InUpgradedHouse = state.InUpgradedHouse,
            SeenEvents = state.SeenEvents,
            Mail = state.Mail,
            DialogueAnswers = state.DialogueAnswers,
            DayEventsKnown = state.DayEventsKnown,
            DayEvents = state.DayEvents
        };
    }

    private static void Evaluate_Unknown()
    {
        var evaluator = new ConditionEvaluator();
        var state = CreateBaseState();

        var result = evaluator.Evaluate(
            new ConditionAstNode
            {
                Type = "Unknown",
                Raw = "mystery condition"
            },
            state
        );

        AssertEqual(null, result.Passed, "Unknown case should return null passed.");
        AssertEqual(true, result.HasUnknown, "Unknown case should flag HasUnknown.");
        AssertEqual(1, result.AtomResults.Count, "Unknown case should still produce one atom result.");
        AssertEqual(null, result.AtomResults[0].Passed, "Unknown atom result should be null.");
    }

    private static RuntimeGameState CreateBaseState(
        bool inUpgradedHouse = true,
        bool? isFestivalDay = false,
        string? spouse = null,
        params string[] visibleNpcNamesHere)
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            DayOfWeek = "Friday",
            Time = 1900,
            Weather = "sunny",
            IsFestivalDay = isFestivalDay,
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            SpouseName = spouse,
            Spouse = spouse,
            FriendshipPoints = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Shane"] = 2200,
                ["Sam"] = 1000
            },
            DatingNpcNames = new HashSet<string>(new[] { "Shane" }, StringComparer.Ordinal),
            VisibleNpcNamesHere = new HashSet<string>(
                visibleNpcNamesHere.Length > 0 ? visibleNpcNamesHere : new[] { "Shane", "Sam" },
                StringComparer.Ordinal
            ),
            InUpgradedHouse = inUpgradedHouse,
            SeenEvents = new HashSet<string>(new[] { "100001" }, StringComparer.Ordinal),
            Mail = new HashSet<string>(new[] { "someMail" }, StringComparer.Ordinal),
            DialogueAnswers = new HashSet<string>(new[] { "ShaneAnswerA" }, StringComparer.Ordinal)
        };
    }

    private static ConditionAstNode CreateAtom(string atomType, string raw, params string[] values)
    {
        return new ConditionAstNode
        {
            Type = "Atom",
            AtomType = atomType,
            Raw = raw,
            Values = values.ToList()
        };
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertContains(string actual, string expectedSubstring, string message)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected substring: {expectedSubstring}; Actual: {actual}");
        }
    }
}
