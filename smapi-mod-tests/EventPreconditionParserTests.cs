using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventPreconditionParserTests
{
    public static void RunAll()
    {
        Parse_MultipleFragments_DefaultToAllOf();
        Parse_NotWrapsExpression();
        Parse_SeasonAlias();
        Parse_DayOfMonthAlias();
        Parse_DayKeyword();
        Parse_DayOfMonthAlias_SupportsMultipleDays();
        Parse_DatingAliasUppercase();
        Parse_DayOfWeekNegatedAliasLowercase();
        Parse_DayOfWeek();
        Parse_DayOfWeekNegatedAlias_MultipleAbbreviatedDays();
        Parse_TimeAlias();
        Parse_WeatherAlias();
        Parse_FriendshipSupportsMultiplePairs();
        Parse_SawEventMultipleIdsUseAnyOf();
        Parse_NotSawEventAliasUsesNotAnyOf();
        Parse_LocalMail();
        Parse_LocalMailAlias();
        Parse_HostMail();
        Parse_HostMailAlias();
        Parse_HostOrLocalMail();
        Parse_HostOrLocalMailAlias();
        Parse_SpouseAliasUppercase();
        Parse_NotSpouseAliasLowercase();
        Parse_NpcVisibleHereAlias();
        Parse_InUpgradedHouseAlias();
        Parse_SpouseBedAlias();
        Parse_YearAlias();
        Parse_NotActiveDialogueEvent();
        Parse_NotUpcomingFestivalAlias();
        Parse_NotRoommateAlias();
        Parse_NotCommunityCenterDoneAlias();
        Parse_ChoseDialogueAnswersMultipleIdsUseAllOf();
        Parse_GameStateQueryAlias();
        Parse_GameStateQuerySeenEvent();
        Parse_GameStateQueryMail();
        Parse_GameStateQuerySeasonDay();
        Parse_NotGameStateQuerySeasonDay();
        Parse_GameStateQueryNpcRelationship();
        Parse_NotGameStateQueryNpcRelationship();
        Parse_TriggerActionConditionSeenEvent();
        Parse_NotFestivalDayAlias();
    }

    private static void Parse_MultipleFragments_DefaultToAllOf()
    {
        var result = Parse("s spring", "t 1800 2200");

        AssertEqual("AllOf", result.ConditionAst.Type, "Root condition type mismatch.");
        AssertEqual(2, result.ConditionAst.Children.Count, "Root child count mismatch.");
        AssertAtom(result.ConditionAst.Children[0], "Season", "s spring", "spring");
        AssertAtom(result.ConditionAst.Children[1], "Time", "t 1800 2200", "1800", "2200");
    }

    private static void Parse_NotWrapsExpression()
    {
        var result = Parse("!w rainy");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "Not node type mismatch.");
        AssertNotNull(node.Operand, "Not node should have an operand.");
        AssertAtom(node.Operand!, "Weather", "w rainy", "rainy");
    }

    private static void Parse_SeasonAlias()
    {
        var result = Parse("s winter");
        AssertAtom(result.ConditionAst.Children.Single(), "Season", "s winter", "winter");
    }

    private static void Parse_DayOfMonthAlias()
    {
        var result = Parse("u 14");
        AssertAtom(result.ConditionAst.Children.Single(), "DayOfMonth", "u 14", "14");
    }

    private static void Parse_DayOfMonthAlias_SupportsMultipleDays()
    {
        var result = Parse("u 12 19 20");
        AssertAtom(result.ConditionAst.Children.Single(), "DayOfMonth", "u 12 19 20", "12", "19", "20");
    }

    private static void Parse_DayKeyword()
    {
        var result = Parse("Day 12");
        AssertAtom(result.ConditionAst.Children.Single(), "DayOfMonth", "Day 12", "12");
    }

    private static void Parse_DatingAliasUppercase()
    {
        var result = Parse("D Shane");
        AssertAtom(result.ConditionAst.Children.Single(), "Dating", "D Shane", "Shane");
    }

    private static void Parse_DayOfWeekNegatedAliasLowercase()
    {
        var result = Parse("d Friday");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "Lowercase d should become Not.");
        AssertNotNull(node.Operand, "Negated DayOfWeek alias should have an operand.");
        AssertAtom(node.Operand!, "DayOfWeek", "d Friday", "Friday");
    }

    private static void Parse_DayOfWeek()
    {
        var result = Parse("DayOfWeek Friday");
        AssertAtom(result.ConditionAst.Children.Single(), "DayOfWeek", "DayOfWeek Friday", "Friday");
    }

    private static void Parse_DayOfWeekNegatedAlias_MultipleAbbreviatedDays()
    {
        var result = Parse("d Mon Tue Wed Thu Sat Sun");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "d alias should negate the day list.");
        AssertNotNull(node.Operand, "Negated multi-day list should have an operand.");
        AssertEqual("AnyOf", node.Operand!.Type, "Multiple days should become AnyOf.");
        AssertEqual(6, node.Operand.Children.Count, "Six weekday tokens expected.");
    }

    private static void Parse_TimeAlias()
    {
        var result = Parse("t 600 1500");
        AssertAtom(result.ConditionAst.Children.Single(), "Time", "t 600 1500", "600", "1500");
    }

    private static void Parse_WeatherAlias()
    {
        var result = Parse("w sunny");
        AssertAtom(result.ConditionAst.Children.Single(), "Weather", "w sunny", "sunny");
    }

    private static void Parse_FriendshipSupportsMultiplePairs()
    {
        var result = Parse("Friendship Shane 2000 Sam 1000");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("AllOf", node.Type, "Friendship multi-pair should become AllOf.");
        AssertEqual(2, node.Children.Count, "Friendship multi-pair child count mismatch.");
        AssertAtom(node.Children[0], "Friendship", "Friendship Shane 2000", "Shane", "2000");
        AssertAtom(node.Children[1], "Friendship", "Friendship Sam 1000", "Sam", "1000");
    }

    private static void Parse_SawEventMultipleIdsUseAnyOf()
    {
        var result = Parse("e 29 30 31");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("AnyOf", node.Type, "SawEvent multi-id should become AnyOf.");
        AssertEqual(3, node.Children.Count, "SawEvent AnyOf child count mismatch.");
        AssertAtom(node.Children[0], "SawEvent", "e 29", "29");
        AssertAtom(node.Children[1], "SawEvent", "e 30", "30");
        AssertAtom(node.Children[2], "SawEvent", "e 31", "31");
    }

    private static void Parse_LocalMail()
    {
        var result = Parse("LocalMail ccVault");
        AssertAtom(result.ConditionAst.Children.Single(), "LocalMail", "LocalMail ccVault", "ccVault");
    }

    private static void Parse_LocalMailAlias()
    {
        var result = Parse("n ccVault");
        AssertAtom(result.ConditionAst.Children.Single(), "LocalMail", "n ccVault", "ccVault");
    }

    private static void Parse_HostMail()
    {
        var result = Parse("HostMail hostFlag");
        AssertAtom(result.ConditionAst.Children.Single(), "HostMail", "HostMail hostFlag", "hostFlag");
    }

    private static void Parse_HostMailAlias()
    {
        var result = Parse("Hn hostFlag");
        AssertAtom(result.ConditionAst.Children.Single(), "HostMail", "Hn hostFlag", "hostFlag");
    }

    private static void Parse_HostOrLocalMail()
    {
        var result = Parse("HostOrLocalMail sharedFlag");
        AssertAtom(result.ConditionAst.Children.Single(), "HostOrLocalMail", "HostOrLocalMail sharedFlag", "sharedFlag");
    }

    private static void Parse_HostOrLocalMailAlias()
    {
        var result = Parse("*n sharedFlag");
        AssertAtom(result.ConditionAst.Children.Single(), "HostOrLocalMail", "*n sharedFlag", "sharedFlag");
    }

    private static void Parse_SpouseAliasUppercase()
    {
        var result = Parse("O Wizard");
        AssertAtom(result.ConditionAst.Children.Single(), "Spouse", "O Wizard", "Wizard");
    }

    private static void Parse_NotSpouseAliasLowercase()
    {
        var result = Parse("o Wizard");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "Lowercase o should become Not.");
        AssertNotNull(node.Operand, "Not spouse alias should have an operand.");
        AssertAtom(node.Operand!, "Spouse", "o Wizard", "Wizard");
    }

    private static void Parse_NpcVisibleHereAlias()
    {
        var result = Parse("p Shane");
        AssertAtom(result.ConditionAst.Children.Single(), "NpcVisibleHere", "p Shane", "Shane");
    }

    private static void Parse_InUpgradedHouseAlias()
    {
        var result = Parse("L");
        AssertAtom(result.ConditionAst.Children.Single(), "InUpgradedHouse", "L");
    }

    private static void Parse_SpouseBedAlias()
    {
        var result = Parse("B");
        AssertAtom(result.ConditionAst.Children.Single(), "SpouseBed", "B");
    }

    private static void Parse_YearAlias()
    {
        var result = Parse("y 4");
        AssertAtom(result.ConditionAst.Children.Single(), "Year", "y 4", "4");
    }

    private static void Parse_NotSawEventAliasUsesNotAnyOf()
    {
        var result = Parse("k 29 30");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "k alias should become Not.");
        AssertNotNull(node.Operand, "Not saw-event alias should have an operand.");
        AssertEqual("AnyOf", node.Operand!.Type, "k alias should wrap an AnyOf saw-event expression.");
        AssertEqual(2, node.Operand.Children.Count, "k alias AnyOf child count mismatch.");
        AssertAtom(node.Operand.Children[0], "SawEvent", "k 29", "29");
        AssertAtom(node.Operand.Children[1], "SawEvent", "k 30", "30");
    }

    private static void Parse_NotActiveDialogueEvent()
    {
        var result = Parse("!ActiveDialogueEvent ccVault");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "Negated ActiveDialogueEvent should become Not.");
        AssertNotNull(node.Operand, "Negated ActiveDialogueEvent should have an operand.");
        AssertAtom(node.Operand!, "ActiveDialogueEvent", "ActiveDialogueEvent ccVault", "ccVault");
    }

    private static void Parse_NotUpcomingFestivalAlias()
    {
        var result = Parse("U");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "U alias should become Not.");
        AssertNotNull(node.Operand, "Not upcoming festival alias should have an operand.");
        AssertAtom(node.Operand!, "UpcomingFestival", "U");
    }

    private static void Parse_NotRoommateAlias()
    {
        var result = Parse("Rf Krobus");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "Rf alias should become Not.");
        AssertNotNull(node.Operand, "Not roommate alias should have an operand.");
        AssertAtom(node.Operand!, "Roommate", "Rf Krobus", "Krobus");
    }

    private static void Parse_NotCommunityCenterDoneAlias()
    {
        var result = Parse("X");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "X alias should become Not.");
        AssertNotNull(node.Operand, "Not community center alias should have an operand.");
        AssertAtom(node.Operand!, "CommunityCenterOrWarehouseDone", "X");
    }

    private static void Parse_ChoseDialogueAnswersMultipleIdsUseAllOf()
    {
        var result = Parse("q answerA answerB");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("AllOf", node.Type, "ChoseDialogueAnswers multi-id should become AllOf.");
        AssertEqual(2, node.Children.Count, "ChoseDialogueAnswers AllOf child count mismatch.");
        AssertAtom(node.Children[0], "ChoseDialogueAnswers", "q answerA", "answerA");
        AssertAtom(node.Children[1], "ChoseDialogueAnswers", "q answerB", "answerB");
    }

    private static void Parse_GameStateQueryAlias()
    {
        var result = Parse("G PLAYER_HAS_ITEM CurrentTool IridiumHoe");
        AssertAtom(
            result.ConditionAst.Children.Single(),
            "GameStateQuery",
            "G PLAYER_HAS_ITEM CurrentTool IridiumHoe",
            "PLAYER_HAS_ITEM",
            "CurrentTool",
            "IridiumHoe");
    }

    private static void Parse_GameStateQuerySeenEvent()
    {
        var result = Parse("G PLAYER_HAS_SEEN_EVENT Current 502261");
        AssertAtom(result.ConditionAst.Children.Single(), "SawEvent", "G PLAYER_HAS_SEEN_EVENT Current 502261", "502261");
    }

    private static void Parse_GameStateQueryMail()
    {
        var result = Parse("G PLAYER_HAS_MAIL Current MaggSamWedding");
        AssertAtom(result.ConditionAst.Children.Single(), "LocalMail", "G PLAYER_HAS_MAIL Current MaggSamWedding", "MaggSamWedding");
    }

    private static void Parse_GameStateQuerySeasonDay()
    {
        var result = Parse("G SEASON_DAY spring 28");
        var node = result.ConditionAst.Children.Single();
        AssertEqual("AllOf", node.Type, "SEASON_DAY should become a season + day compound.");
        AssertEqual(2, node.Children.Count, "SEASON_DAY child count mismatch.");
        AssertAtom(node.Children[0], "Season", "G SEASON_DAY spring 28", "spring");
        AssertAtom(node.Children[1], "DayOfMonth", "G SEASON_DAY spring 28", "28");
    }

    private static void Parse_NotGameStateQuerySeasonDay()
    {
        var result = Parse("!G SEASON_DAY summer 28");
        var node = result.ConditionAst.Children.Single();
        AssertEqual("Not", node.Type, "!G should negate the SEASON_DAY compound.");
        AssertEqual("AllOf", node.Operand!.Type, "Inner SEASON_DAY should be AllOf.");
    }

    private static void Parse_GameStateQueryNpcRelationship()
    {
        var result = Parse("G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married");
        AssertAtom(
            result.ConditionAst.Children.Single(),
            "Relationship",
            "G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married",
            "Sebastian",
            "Engaged",
            "Married");
    }

    private static void Parse_NotGameStateQueryNpcRelationship()
    {
        var result = Parse("!G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married");
        var node = result.ConditionAst.Children.Single();
        AssertEqual("Not", node.Type, "!G relationship should be negated.");
        AssertAtom(
            node.Operand!,
            "Relationship",
            "!G PLAYER_NPC_RELATIONSHIP Current Sebastian Engaged Married",
            "Sebastian",
            "Engaged",
            "Married");
    }

    private static void Parse_TriggerActionConditionSeenEvent()
    {
        var result = Parse("PLAYER_HAS_SEEN_EVENT Current 502261");
        AssertAtom(result.ConditionAst.Children.Single(), "SawEvent", "PLAYER_HAS_SEEN_EVENT Current 502261", "502261");
    }

    private static void Parse_NotFestivalDayAlias()
    {
        var result = Parse("F");
        var node = result.ConditionAst.Children.Single();

        AssertEqual("Not", node.Type, "F alias should become Not.");
        AssertNotNull(node.Operand, "F alias should wrap a FestivalDay operand.");
        AssertAtom(node.Operand!, "FestivalDay", "F");
    }

    private static EventPreconditionParseResult Parse(params string[] fragments)
    {
        return new EventPreconditionParser().Parse(fragments);
    }

    private static void AssertAtom(ConditionAstNode node, string atomType, string raw, params string[] values)
    {
        AssertEqual("Atom", node.Type, "Atom node type mismatch.");
        AssertEqual(atomType, node.AtomType, "Atom kind mismatch.");
        AssertEqual(raw, node.Raw, "Atom raw mismatch.");
        AssertSequenceEqual(values, node.Values, "Atom values mismatch.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected: [{string.Join(", ", expected)}]; Actual: [{string.Join(", ", actual)}]"
            );
        }
    }
}
