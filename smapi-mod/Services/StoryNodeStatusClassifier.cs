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

        if (node.EventKind != StoryNodeEventKind.RegularLocationEvent)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                MapEventKindToStatus(node.EventKind),
                DescribeNonTriggerableEvent(node));
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
                BuildPatchWhenUnknownReason(unknownPatchWhenConditions));
        }

        var failedPatchWhenConditions = node.PatchWhenConditions
            .Where(condition => condition.IsKnown && condition.Passed == false && condition.IsProgressionSensitive)
            .ToList();
        if (failedPatchWhenConditions.Count > 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.Locked,
                $"Patch-level progression conditions failed: {string.Join("; ", failedPatchWhenConditions.Select(condition => condition.Reason))}"
            );
        }

        var failedContextPatchWhenConditions = node.PatchWhenConditions
            .Where(condition => condition.IsKnown && condition.Passed == false && condition.IsContextSensitive)
            .ToList();
        if (failedContextPatchWhenConditions.Count > 0)
        {
            return this.CreateEvaluation(
                node,
                conditionResult,
                StoryNodeStatus.AvailableLater,
                $"Patch-level context conditions are not currently met: {string.Join("; ", failedContextPatchWhenConditions.Select(condition => condition.Reason))}"
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
                .Select(atom =>
                {
                    if (!string.IsNullOrWhiteSpace(atom.ReasonZh))
                    {
                        return atom.ReasonZh!;
                    }

                    return string.IsNullOrWhiteSpace(atom.Raw) ? atom.AtomType : atom.Raw;
                })
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
                $"{string.Join("; ", unknownParts)}。无法据此判断可触发状态。");
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
            EventKind = node.EventKind,
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

    private static StoryNodeStatus MapEventKindToStatus(StoryNodeEventKind eventKind)
    {
        return eventKind switch
        {
            StoryNodeEventKind.BranchTarget => StoryNodeStatus.BranchTarget,
            StoryNodeEventKind.SpecialGameEvent => StoryNodeStatus.SpecialEvent,
            StoryNodeEventKind.DialogueOnly => StoryNodeStatus.NonTriggerable,
            StoryNodeEventKind.InvalidOrUnsupported => StoryNodeStatus.NonTriggerable,
            _ => StoryNodeStatus.NonTriggerable
        };
    }

    private static string DescribeNonTriggerableEvent(StoryNode node)
    {
        return node.EventKind switch
        {
            StoryNodeEventKind.BranchTarget =>
                $"Event id '{node.EventId}' is a fork/branch target or answer script, not a regular location-entry event.",
            StoryNodeEventKind.SpecialGameEvent =>
                $"Event id '{node.EventId}' is a game-triggered special event, not a regular location-entry event.",
            StoryNodeEventKind.DialogueOnly =>
                $"Event id '{node.EventId}' has no location-entry preconditions and is treated as dialogue-only metadata.",
            StoryNodeEventKind.InvalidOrUnsupported =>
                $"Event id '{node.EventId}' is not supported as a regular triggerable event.",
            _ => $"Event id '{node.EventId}' is not classified as a regular location-entry event."
        };
    }

    private static string BuildPatchWhenUnknownReason(IReadOnlyList<PatchWhenCondition> unknownPatchWhenConditions)
    {
        var runtimeMissing = unknownPatchWhenConditions
            .Where(condition => string.Equals(condition.UnknownKind, "runtimeMissing", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var complexQuery = unknownPatchWhenConditions
            .Where(condition => string.Equals(condition.UnknownKind, "complexQueryUnsupported", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var externalToken = unknownPatchWhenConditions
            .Where(condition => string.Equals(condition.UnknownKind, "externalTokenMissing", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var randomToken = unknownPatchWhenConditions
            .Where(condition => string.Equals(condition.UnknownKind, "randomTokenUnsupported", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var parseUnknown = unknownPatchWhenConditions
            .Where(condition =>
                string.IsNullOrWhiteSpace(condition.UnknownKind)
                || string.Equals(condition.UnknownKind, "parseUnknown", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var parts = new List<string>();
        if (runtimeMissing.Count > 0)
        {
            parts.Add($"运行时状态缺失：{string.Join("; ", runtimeMissing.Select(FormatPatchWhenReasonZh))}");
        }

        if (complexQuery.Count > 0 || randomToken.Count > 0)
        {
            parts.Add("随机/概率条件暂不展开");
        }

        if (externalToken.Count > 0)
        {
            parts.Add($"外部 token 未导出：{string.Join("; ", externalToken.Select(FormatPatchWhenReasonZh))}");
        }

        if (parseUnknown.Count > 0)
        {
            parts.Add($"解析器暂不支持：{string.Join("; ", parseUnknown.Select(FormatPatchWhenCondition))}");
        }

        if (parts.Count == 0)
        {
            parts.Add($"Patch-level When conditions are not evaluated: {string.Join("; ", unknownPatchWhenConditions.Select(FormatPatchWhenCondition))}");
        }

        return $"{string.Join("; ", parts)}。无法据此判断可触发状态。";
    }

    private static string FormatPatchWhenCondition(PatchWhenCondition condition)
    {
        return string.IsNullOrWhiteSpace(condition.Value)
            ? condition.Key
            : $"{condition.Key}={condition.Value}";
    }

    private static string FormatPatchWhenReasonZh(PatchWhenCondition condition)
    {
        return !string.IsNullOrWhiteSpace(condition.ReasonZh)
            ? condition.ReasonZh
            : condition.Reason;
    }
}
