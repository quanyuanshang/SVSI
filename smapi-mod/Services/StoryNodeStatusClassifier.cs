using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class StoryNodeStatusClassifier
{
    public StoryNodeEvaluation Classify(
        StoryNode node,
        RuntimeGameState state,
        ConditionEvaluationResult conditionResult)
    {
        var seenEvents = state.SeenEvents ?? new HashSet<string>(StringComparer.Ordinal);
        if (seenEvents.Contains(node.EventId))
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Triggered,
                $"Event {node.EventId} has already been seen."
            );
        }

        if (!IsLocationTriggerableEventId(node.EventId) && node.RawPreconditions.Count == 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Unknown,
                $"Event id '{node.EventId}' is non-numeric and has no preconditions; it is likely a fork/branch target or a game-triggered special event (e.g., PlayerKilled) rather than a regular location-entry event."
            );
        }

        var unknownPatchWhenConditions = node.PatchWhenConditions
            .Where(condition => !condition.IsKnown)
            .ToList();
        if (unknownPatchWhenConditions.Count > 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Unknown,
                $"Patch-level When conditions are not evaluated: {string.Join("; ", unknownPatchWhenConditions.Select(FormatPatchWhenCondition))}. Cannot safely determine status."
            );
        }

        var progressionFailures = conditionResult.AtomResults
            .Where(atom => atom.Passed == false && atom.IsProgressionSensitive)
            .ToList();

        if (conditionResult.HasUnknown && progressionFailures.Count == 0)
        {
            var unknownParts = new List<string>();

            if (node.UnknownFragments.Count > 0)
            {
                unknownParts.Add($"Unknown fragments: {string.Join(", ", node.UnknownFragments)}");
            }

            var unknownAtoms = conditionResult.AtomResults
                .Where(atom => atom.Passed is null)
                .Select(atom => string.IsNullOrWhiteSpace(atom.Raw) ? atom.AtomType : atom.Raw)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (unknownAtoms.Count > 0)
            {
                unknownParts.Add($"Unknown atoms: {string.Join(", ", unknownAtoms)}");
            }

            if (unknownParts.Count == 0)
            {
                unknownParts.Add("Condition result contains unknown values.");
            }

            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Unknown,
                $"{string.Join("; ", unknownParts)} Cannot safely determine status."
            );
        }

        if (progressionFailures.Count > 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Locked,
                $"Progression conditions failed: {string.Join("; ", progressionFailures.Select(atom => atom.Reason))}"
            );
        }

        var contextFailures = conditionResult.AtomResults
            .Where(atom => atom.Passed == false && atom.IsContextSensitive)
            .ToList();

        if (contextFailures.Count > 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.AvailableLater,
                $"Context conditions not currently met: {string.Join("; ", contextFailures.Select(atom => atom.Reason))}"
            );
        }

        if (!string.Equals(node.Location, state.CurrentLocation, StringComparison.OrdinalIgnoreCase))
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.AvailableLater,
                $"Progression conditions are met, but player is currently at {state.CurrentLocation}, event location is {node.Location}."
            );
        }

        return this.CreateEvaluation(
            node,
            conditionResult,
            StoryNodeStatus.Current,
            "All known conditions are satisfied and player is at the event location."
        );
    }

    private StoryNodeEvaluation CreateEvaluation(
        StoryNode node,
        ConditionEvaluationResult conditionResult,
        StoryNodeStatus status,
        string statusReason)
    {
        return new StoryNodeEvaluation
        {
            NodeId = node.NodeId,
            EventId = node.EventId,
            SourceModId = node.SourceModId,
            SourceModName = node.SourceModName,
            Location = node.Location,
            RawKey = node.RawKey,
            RawPreconditions = node.RawPreconditions,
            UnknownFragments = node.UnknownFragments,
            RawScriptPreview = node.RawScriptPreview,
            PatchWhenConditions = node.PatchWhenConditions,
            Status = status,
            StatusReason = statusReason,
            ConditionResult = conditionResult,
            EvidenceRefs = node.EvidenceRefs,
            RelatedDialogueRefs = node.RelatedDialogueRefs,
            RelatedEventChoiceRefs = node.RelatedEventChoiceRefs
        };
    }

    private static string FormatPatchWhenCondition(PatchWhenCondition condition)
    {
        return string.IsNullOrWhiteSpace(condition.Value)
            ? condition.Key
            : $"{condition.Key}={condition.Value}";
    }

    private static bool IsLocationTriggerableEventId(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return false;
        }

        foreach (var character in eventId)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
