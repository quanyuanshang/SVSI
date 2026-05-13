using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class StoryStateEvaluatorTests
{
    public static void RunAll()
    {
        Evaluate_FiveNodes_ProducesExpectedCountsAndOrder();
        Evaluate_NodeWithUnknownPatchWhen_IsUnknownNotCurrent();
        Evaluate_NonNumericEventIdWithoutPreconditions_IsUnknownNotCurrent();
    }

    private static void Evaluate_NonNumericEventIdWithoutPreconditions_IsUnknownNotCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "PlayerKilled",
            sourceModId: "Tests.SpecialTrigger",
            sourceModName: "Special Trigger Pack",
            location: "Town",
            conditionAst: new ConditionAstNode { Type = "AllOf" }
        );

        var report = evaluator.Evaluate(new[] { node }, state);
        var evaluation = report.Nodes.Single();

        AssertEqual(StoryNodeStatus.Unknown, evaluation.Status, "Non-numeric event id without preconditions must not be classified as Current.");
        AssertTrue(
            evaluation.StatusReason.Contains("game-triggered", StringComparison.OrdinalIgnoreCase) ||
            evaluation.StatusReason.Contains("special", StringComparison.OrdinalIgnoreCase) ||
            evaluation.StatusReason.Contains("non-numeric", StringComparison.OrdinalIgnoreCase),
            "Status reason should explain the entry is a special / non-numeric trigger."
        );
    }

    private static void Evaluate_FiveNodes_ProducesExpectedCountsAndOrder()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var nodes = new[]
        {
            CreateTriggeredNode(),
            CreateCurrentNode(),
            CreateAvailableLaterNode(),
            CreateLockedNode(),
            CreateUnknownNode()
        };

        var report = evaluator.Evaluate(nodes, state);

        AssertEqual(5, report.TotalNodeCount, "TotalNodeCount mismatch.");
        AssertEqual(1, report.StatusCounts["Triggered"], "Triggered count mismatch.");
        AssertEqual(1, report.StatusCounts["Current"], "Current count mismatch.");
        AssertEqual(1, report.StatusCounts["AvailableLater"], "AvailableLater count mismatch.");
        AssertEqual(1, report.StatusCounts["Locked"], "Locked count mismatch.");
        AssertEqual(1, report.StatusCounts["Unknown"], "Unknown count mismatch.");

        AssertEqual("200001", report.Nodes[0].EventId, "Current node should sort first.");
        AssertEqual(StoryNodeStatus.Current, report.Nodes[0].Status, "First node should be Current.");

        AssertEqual("200002", report.Nodes[1].EventId, "AvailableLater node should sort second.");
        AssertEqual(StoryNodeStatus.AvailableLater, report.Nodes[1].Status, "Second node should be AvailableLater.");

        AssertEqual("200003", report.Nodes[2].EventId, "Locked node should sort third.");
        AssertEqual(StoryNodeStatus.Locked, report.Nodes[2].Status, "Third node should be Locked.");

        AssertEqual("200004", report.Nodes[3].EventId, "Unknown node should sort fourth.");
        AssertEqual(StoryNodeStatus.Unknown, report.Nodes[3].Status, "Fourth node should be Unknown.");

        AssertEqual("100001", report.Nodes[4].EventId, "Triggered node should sort last.");
        AssertEqual(StoryNodeStatus.Triggered, report.Nodes[4].Status, "Last node should be Triggered.");
    }

    private static void Evaluate_NodeWithUnknownPatchWhen_IsUnknownNotCurrent()
    {
        var evaluator = new StoryStateEvaluator();
        var state = CreateBaseState();
        var node = CreateNode(
            eventId: "300001",
            sourceModId: "Tests.PatchWhen",
            sourceModName: "Patch When Pack",
            location: "Town",
            conditionAst: CreateAtom("Time", "t 600 2400", "600", "2400")
        );
        node.PatchWhenConditions.Add(new PatchWhenCondition
        {
            Key = "Relationship:Alex",
            Value = "Engaged",
            IsKnown = false,
            Reason = "Patch-level When condition is not evaluated."
        });

        var report = evaluator.Evaluate(new[] { node }, state);

        AssertEqual(StoryNodeStatus.Unknown, report.Nodes.Single().Status, "Unknown CP When should prevent Current status.");
        AssertTrue(
            report.Nodes.Single().StatusReason.Contains("Relationship:Alex", StringComparison.Ordinal),
            "Status reason should mention the unknown CP When condition."
        );
    }

    private static StoryNode CreateTriggeredNode()
    {
        return CreateNode(
            eventId: "100001",
            sourceModId: "Tests.Triggered",
            sourceModName: "Triggered Pack",
            location: "Town",
            conditionAst: CreateAtom("Season", "Season fall", "fall")
        );
    }

    private static StoryNode CreateCurrentNode()
    {
        return CreateNode(
            eventId: "200001",
            sourceModId: "Tests.Current",
            sourceModName: "Alpha Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "AllOf",
                Children =
                {
                    CreateAtom("Friendship", "f Shane 2000", "Shane", "2000"),
                    CreateAtom("Season", "Season fall", "fall")
                }
            }
        );
    }

    private static StoryNode CreateAvailableLaterNode()
    {
        return CreateNode(
            eventId: "200002",
            sourceModId: "Tests.AvailableLater",
            sourceModName: "Beta Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "AllOf",
                Children =
                {
                    CreateAtom("Friendship", "f Shane 2000", "Shane", "2000"),
                    CreateAtom("Time", "t 600 1200", "600", "1200")
                }
            }
        );
    }

    private static StoryNode CreateLockedNode()
    {
        return CreateNode(
            eventId: "200003",
            sourceModId: "Tests.Locked",
            sourceModName: "Gamma Pack",
            location: "Town",
            conditionAst: CreateAtom("Friendship", "f Shane 3000", "Shane", "3000")
        );
    }

    private static StoryNode CreateUnknownNode()
    {
        return CreateNode(
            eventId: "200004",
            sourceModId: "Tests.Unknown",
            sourceModName: "Omega Pack",
            location: "Town",
            conditionAst: new ConditionAstNode
            {
                Type = "Unknown",
                Raw = "mystery condition"
            },
            unknownFragments: new[] { "mystery condition" }
        );
    }

    private static StoryNode CreateNode(
        string eventId,
        string sourceModId,
        string sourceModName,
        string location,
        ConditionAstNode conditionAst,
        IEnumerable<string>? unknownFragments = null)
    {
        return new StoryNode
        {
            NodeId = $"story-node:{eventId}",
            EventId = eventId,
            SourceModId = sourceModId,
            SourceModName = sourceModName,
            AssetTarget = $"Data/Events/{location}",
            Location = location,
            RawKey = eventId,
            ConditionAst = conditionAst,
            UnknownFragments = unknownFragments?.ToList() ?? new List<string>(),
            EvidenceRefs =
            {
                new EvidenceRef
                {
                    Kind = "test",
                    SourcePath = "test.json",
                    JsonPath = "$.nodes[0]"
                }
            }
        };
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

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
