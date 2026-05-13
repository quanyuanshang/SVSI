using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class ConditionEvaluatorTests
{
    public static void RunAll()
    {
        Evaluate_Season_TrueAndFalse();
        Evaluate_Time_TrueAndFalse();
        Evaluate_Friendship_TrueAndFalse();
        Evaluate_SawEvent_TrueAndFalse();
        Evaluate_Mail_TrueAndFalse();
        Evaluate_ChoseDialogueAnswers_TrueAndFalse();
        Evaluate_AllOf();
        Evaluate_AnyOf();
        Evaluate_Not();
        Evaluate_Unknown();
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

    private static RuntimeGameState CreateBaseState()
    {
        return new RuntimeGameState
        {
            Year = 1,
            Season = "fall",
            DayOfMonth = 12,
            DayOfWeek = "Friday",
            Time = 1900,
            Weather = "sunny",
            CurrentLocation = "Town",
            PlayerName = "MockFarmer",
            FriendshipPoints = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Shane"] = 2200,
                ["Sam"] = 1000
            },
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
