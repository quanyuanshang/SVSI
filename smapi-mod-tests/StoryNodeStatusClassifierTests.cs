using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class StoryNodeStatusClassifierTests
{
    public static void RunAll()
    {
        Classify_SeenEvent_ReturnsTriggered();
        Classify_AllPassAndSameLocation_ReturnsCurrent();
        Classify_AllPassAndDifferentLocation_ReturnsAvailableLater();
        Classify_TimeFailedButFriendshipPassed_ReturnsAvailableLater();
        Classify_FriendshipFailed_ReturnsLocked();
        Classify_UnknownOnly_ReturnsUnknown();
        Classify_UnknownAndFriendshipFailed_ReturnsLocked();
    }

    private static void Classify_SeenEvent_ReturnsTriggered()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("100001", "Town");
        var state = CreateState("Town", seenEvents: new[] { "100001" });
        var conditionResult = CreateConditionResult(
            passed: true,
            hasUnknown: false,
            reason: "All conditions passed."
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.Triggered, evaluation.Status, "Seen event should be Triggered.");
        AssertContains(evaluation.StatusReason, "already been seen", "Triggered reason mismatch.");
    }

    private static void Classify_AllPassAndSameLocation_ReturnsCurrent()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200001", "Town");
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: true,
            hasUnknown: false,
            reason: "All conditions passed.",
            CreateAtomResult("Friendship", true, false, true, "Friendship matched: Shane has 2200, requires 2000")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.Current, evaluation.Status, "All pass + same location should be Current.");
    }

    private static void Classify_AllPassAndDifferentLocation_ReturnsAvailableLater()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200002", "Beach");
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: true,
            hasUnknown: false,
            reason: "All conditions passed.",
            CreateAtomResult("Friendship", true, false, true, "Friendship matched: Shane has 2200, requires 2000")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.AvailableLater, evaluation.Status, "Different location should be AvailableLater.");
        AssertContains(evaluation.StatusReason, "currently at Town", "Location mismatch reason should mention current location.");
    }

    private static void Classify_TimeFailedButFriendshipPassed_ReturnsAvailableLater()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200003", "Town");
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: false,
            hasUnknown: false,
            reason: "Time failed.",
            CreateAtomResult("Friendship", true, false, true, "Friendship matched: Shane has 2200, requires 2000"),
            CreateAtomResult("Time", false, true, false, "Time failed: current 1900 is outside [600, 1200]")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.AvailableLater, evaluation.Status, "Context failure should be AvailableLater.");
        AssertContains(evaluation.StatusReason, "Time failed", "AvailableLater reason should include time failure.");
    }

    private static void Classify_FriendshipFailed_ReturnsLocked()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200004", "Town");
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: false,
            hasUnknown: false,
            reason: "Friendship failed.",
            CreateAtomResult("Friendship", false, false, true, "Friendship failed: Shane has 1200, requires 2000")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.Locked, evaluation.Status, "Progression failure should be Locked.");
        AssertContains(evaluation.StatusReason, "Friendship failed", "Locked reason should include friendship failure.");
    }

    private static void Classify_UnknownOnly_ReturnsUnknown()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200005", "Town", unknownFragments: new[] { "mystery condition" });
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: null,
            hasUnknown: true,
            reason: "Unknown condition.",
            CreateAtomResult("Unknown", null, false, false, "Unknown atom cannot be evaluated.", "mystery condition")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.Unknown, evaluation.Status, "Unknown-only case should be Unknown.");
        AssertContains(evaluation.StatusReason, "Cannot safely determine status", "Unknown reason mismatch.");
    }

    private static void Classify_UnknownAndFriendshipFailed_ReturnsLocked()
    {
        var classifier = new StoryNodeStatusClassifier();
        var node = CreateNode("200006", "Town", unknownFragments: new[] { "mystery condition" });
        var state = CreateState("Town");
        var conditionResult = CreateConditionResult(
            passed: null,
            hasUnknown: true,
            reason: "Unknown plus friendship failure.",
            CreateAtomResult("Unknown", null, false, false, "Unknown atom cannot be evaluated.", "mystery condition"),
            CreateAtomResult("Friendship", false, false, true, "Friendship failed: Shane has 1200, requires 2000")
        );

        var evaluation = classifier.Classify(node, state, conditionResult);

        AssertEqual(StoryNodeStatus.Locked, evaluation.Status, "Progression failure should beat unknown.");
        AssertContains(evaluation.StatusReason, "Friendship failed", "Locked reason should still include friendship failure.");
    }

    private static StoryNode CreateNode(string eventId, string location, IEnumerable<string>? unknownFragments = null)
    {
        return new StoryNode
        {
            NodeId = $"story-node:{eventId}",
            EventId = eventId,
            SourceModId = "Tests.MockPack",
            SourceModName = "Mock Pack",
            Location = location,
            EvidenceRefs =
            {
                new EvidenceRef
                {
                    Kind = "test",
                    SourcePath = "test.json",
                    JsonPath = "$.test"
                }
            },
            RelatedDialogueRefs =
            {
                new RelatedDialogueRef
                {
                    NpcName = "Shane",
                    DialogueKey = "Question_0",
                    ResponseId = "ShaneAnswerA",
                    PreviewText = "Question preview",
                    SourceModId = "Tests.Dialogue"
                }
            },
            RelatedEventChoiceRefs =
            {
                new RelatedEventChoiceRef
                {
                    EventId = "5000",
                    AssetTarget = "Data/Events/Town",
                    Location = "Town",
                    RawKey = "5000/q ShaneAnswerA",
                    ResponseId = "ShaneAnswerA",
                    PreviewText = "Event preview",
                    SourceModId = "Tests.Events",
                    SourceModName = "Mock Event Pack"
                }
            },
            UnknownFragments = unknownFragments?.ToList() ?? new List<string>()
        };
    }

    private static RuntimeGameState CreateState(string currentLocation, IEnumerable<string>? seenEvents = null)
    {
        return new RuntimeGameState
        {
            CurrentLocation = currentLocation,
            SeenEvents = new HashSet<string>(seenEvents ?? Array.Empty<string>(), StringComparer.Ordinal)
        };
    }

    private static ConditionEvaluationResult CreateConditionResult(
        bool? passed,
        bool hasUnknown,
        string reason,
        params ConditionAtomResult[] atomResults)
    {
        return new ConditionEvaluationResult
        {
            Passed = passed,
            HasUnknown = hasUnknown,
            Reason = reason,
            AtomResults = atomResults.ToList()
        };
    }

    private static ConditionAtomResult CreateAtomResult(
        string atomType,
        bool? passed,
        bool isContextSensitive,
        bool isProgressionSensitive,
        string reason,
        string? raw = null)
    {
        return new ConditionAtomResult
        {
            Raw = raw ?? atomType,
            AtomType = atomType,
            Passed = passed,
            IsContextSensitive = isContextSensitive,
            IsProgressionSensitive = isProgressionSensitive,
            Reason = reason
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
