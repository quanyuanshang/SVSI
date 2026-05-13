using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class StoryStateEvaluator
{
    private readonly ConditionEvaluator conditionEvaluator;
    private readonly StoryNodeStatusClassifier statusClassifier;

    public StoryStateEvaluator()
        : this(new ConditionEvaluator(), new StoryNodeStatusClassifier())
    {
    }

    public StoryStateEvaluator(
        ConditionEvaluator conditionEvaluator,
        StoryNodeStatusClassifier statusClassifier)
    {
        this.conditionEvaluator = conditionEvaluator;
        this.statusClassifier = statusClassifier;
    }

    public StoryStateEvaluationReport Evaluate(IEnumerable<StoryNode> nodes, RuntimeGameState state)
    {
        var evaluations = nodes
            .Select(node =>
            {
                var conditionResult = this.conditionEvaluator.Evaluate(node.ConditionAst, state);
                return this.statusClassifier.Classify(node, state, conditionResult);
            })
            .OrderBy(evaluation => GetStatusSortOrder(evaluation.Status))
            .ThenBy(evaluation => evaluation.SourceModName, StringComparer.Ordinal)
            .ThenBy(evaluation => evaluation.Location, StringComparer.Ordinal)
            .ThenBy(evaluation => evaluation.EventId, StringComparer.Ordinal)
            .ToList();

        var statusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (StoryNodeStatus status in Enum.GetValues(typeof(StoryNodeStatus)))
        {
            statusCounts[status.ToString()] = 0;
        }

        foreach (var evaluation in evaluations)
        {
            var key = evaluation.Status.ToString();
            statusCounts[key] = statusCounts.GetValueOrDefault(key) + 1;
        }

        return new StoryStateEvaluationReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RuntimeState = state,
            TotalNodeCount = evaluations.Count,
            StatusCounts = statusCounts,
            Nodes = evaluations
        };
    }

    private static int GetStatusSortOrder(StoryNodeStatus status)
    {
        return status switch
        {
            StoryNodeStatus.Current => 0,
            StoryNodeStatus.AvailableLater => 1,
            StoryNodeStatus.Locked => 2,
            StoryNodeStatus.Unknown => 3,
            StoryNodeStatus.Triggered => 4,
            _ => int.MaxValue
        };
    }
}
