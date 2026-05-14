using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class StoryStateEvaluator
{
    private static readonly string[] FarmerCheaterModIds =
    {
        "aedenthorn.FreeLove",
        "ApryllForever.PolyamorySweetLove",
        "EnderTedi.Polyamory"
    };

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

    public StoryStateEvaluationReport Evaluate(
        IEnumerable<StoryNode> nodes,
        RuntimeGameState state,
        TranslationCatalog? translationCatalog = null)
    {
        var evaluations = nodes
            .Select(node =>
            {
                var conditionResult = this.conditionEvaluator.Evaluate(node.ConditionAst, state);
                var evaluatedNode = this.WithEvaluatedPatchWhenConditions(node, state);
                return this.statusClassifier.Classify(evaluatedNode, state, conditionResult);
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
            TranslationCatalog = translationCatalog ?? new TranslationCatalog(),
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

    private StoryNode WithEvaluatedPatchWhenConditions(StoryNode node, RuntimeGameState state)
    {
        if (node.PatchWhenConditions.Count == 0)
        {
            return node;
        }

        return new StoryNode
        {
            NodeId = node.NodeId,
            EventId = node.EventId,
            SourceModId = node.SourceModId,
            SourceModName = node.SourceModName,
            AssetTarget = node.AssetTarget,
            Location = node.Location,
            RawKey = node.RawKey,
            RawPreconditions = node.RawPreconditions,
            PatchWhenConditions = this.EvaluatePatchWhenConditions(node, state),
            ConditionAst = node.ConditionAst,
            UnknownFragments = node.UnknownFragments,
            RawScriptPreview = node.RawScriptPreview,
            EvidenceRefs = node.EvidenceRefs,
            RelatedDialogueRefs = node.RelatedDialogueRefs,
            RelatedEventChoiceRefs = node.RelatedEventChoiceRefs
        };
    }

    private List<PatchWhenCondition> EvaluatePatchWhenConditions(StoryNode node, RuntimeGameState state)
    {
        var results = new List<PatchWhenCondition>(node.PatchWhenConditions.Count);
        foreach (var condition in node.PatchWhenConditions)
        {
            results.Add(this.EvaluatePatchWhenCondition(node, condition, state));
        }

        return results;
    }

    private PatchWhenCondition EvaluatePatchWhenCondition(
        StoryNode node,
        PatchWhenCondition condition,
        RuntimeGameState state)
    {
        if (TryEvaluateRelationshipCondition(condition, state, out var relationshipEvaluation))
        {
            return relationshipEvaluation;
        }

        if (TryEvaluateHeartsCondition(condition, state, out var heartsEvaluation))
        {
            return heartsEvaluation;
        }

        if (TryEvaluateHasModCondition(condition, state, out var hasModEvaluation))
        {
            return hasModEvaluation;
        }

        if (TryEvaluateFarmerCheaterCondition(node, condition, state, out var farmerCheaterEvaluation))
        {
            return farmerCheaterEvaluation;
        }

        return condition;
    }

    private static bool TryEvaluateRelationshipCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out var argument, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "Relationship", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        var currentRelationship = GetRelationshipState(argument, state);
        var currentLabel = currentRelationship ?? "None";
        bool passed;
        string expectation;

        if (string.Equals(modifierOperator, "contains", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(modifierValue))
        {
            var wantContains = TryParseBoolean(condition.Value, out var parsedBool) ? parsedBool : true;
            var contains = string.Equals(currentLabel, modifierValue, StringComparison.OrdinalIgnoreCase);
            passed = contains == wantContains;
            expectation = wantContains
                ? $"relationship contains {modifierValue}"
                : $"relationship does not contain {modifierValue}";
        }
        else
        {
            var expectedStates = SplitCsv(condition.Value);
            if (expectedStates.Count == 0)
            {
                return false;
            }

            passed = expectedStates.Any(value => string.Equals(value, currentLabel, StringComparison.OrdinalIgnoreCase));
            expectation = $"relationship is one of [{string.Join(", ", expectedStates)}]";
        }

        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"Relationship matched: {argument} is {currentLabel}, expected {expectation}.",
            $"Relationship failed: {argument} is {currentLabel}, expected {expectation}.");
        return true;
    }

    private static bool TryEvaluateHeartsCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out var argument, out _, out _)
            || !string.Equals(token, "Hearts", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        if (!int.TryParse(condition.Value, out var requiredHearts))
        {
            return false;
        }

        state.FriendshipPoints.TryGetValue(argument, out var currentPoints);
        var currentHearts = currentPoints / 250;
        var passed = currentHearts == requiredHearts;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"Hearts matched: {argument} has {currentHearts} hearts.",
            $"Hearts failed: {argument} has {currentHearts} hearts, requires exactly {requiredHearts}.");
        return true;
    }

    private static bool TryEvaluateHasModCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "HasMod", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(modifierOperator, "contains", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(modifierValue))
        {
            return false;
        }

        if (!TryParseBoolean(condition.Value, out var expectedInstalled))
        {
            return false;
        }

        var candidateModIds = SplitCsv(modifierValue);
        var matchedMods = candidateModIds
            .Where(candidate => state.InstalledModIds.Contains(candidate))
            .ToList();
        var hasAny = matchedMods.Count > 0;
        var passed = hasAny == expectedInstalled;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            hasAny
                ? $"HasMod matched: installed [{string.Join(", ", matchedMods)}]."
                : "HasMod matched: none of the candidate mod ids are installed.",
            hasAny
                ? $"HasMod failed: installed [{string.Join(", ", matchedMods)}]."
                : $"HasMod failed: none of [{string.Join(", ", candidateModIds)}] are installed.");
        return true;
    }

    private static bool TryEvaluateFarmerCheaterCondition(
        StoryNode node,
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!string.Equals(condition.Key, "FarmerCheater", StringComparison.OrdinalIgnoreCase)
            || !node.SourceModId.StartsWith("maggplays.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = condition.Value.Trim();
        if (!string.Equals(expected, "yes", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(expected, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var matchedMods = FarmerCheaterModIds
            .Where(candidate => state.InstalledModIds.Contains(candidate))
            .ToList();
        var isCheater = matchedMods.Count > 0;
        var passed = string.Equals(expected, isCheater ? "yes" : "no", StringComparison.OrdinalIgnoreCase);
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            isCheater
                ? $"FarmerCheater matched: detected polyamory mods [{string.Join(", ", matchedMods)}]."
                : "FarmerCheater matched: no supported polyamory mods detected.",
            isCheater
                ? $"FarmerCheater failed: detected polyamory mods [{string.Join(", ", matchedMods)}]."
                : "FarmerCheater failed: no supported polyamory mods detected.");
        return true;
    }

    private static PatchWhenCondition CreateEvaluatedPatchWhenCondition(
        PatchWhenCondition original,
        bool passed,
        string successReason,
        string failureReason)
    {
        return new PatchWhenCondition
        {
            Key = original.Key,
            Value = original.Value,
            RawValue = original.RawValue,
            IsKnown = true,
            Passed = passed,
            IsProgressionSensitive = true,
            Reason = passed ? successReason : failureReason
        };
    }

    private static bool TryParseTokenAndModifier(
        string rawKey,
        out string token,
        out string argument,
        out string modifierOperator,
        out string modifierValue)
    {
        token = string.Empty;
        argument = string.Empty;
        modifierOperator = string.Empty;
        modifierValue = string.Empty;

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return false;
        }

        var segments = rawKey.Split('|', 2, StringSplitOptions.TrimEntries);
        var tokenSegment = segments[0].Trim();
        if (string.IsNullOrWhiteSpace(tokenSegment))
        {
            return false;
        }

        var colonIndex = tokenSegment.IndexOf(':');
        if (colonIndex >= 0)
        {
            token = tokenSegment[..colonIndex].Trim();
            argument = tokenSegment[(colonIndex + 1)..].Trim();
        }
        else
        {
            token = tokenSegment.Trim();
        }

        if (segments.Length == 2)
        {
            var modifierSegment = segments[1].Trim();
            var equalsIndex = modifierSegment.IndexOf('=');
            if (equalsIndex >= 0)
            {
                modifierOperator = modifierSegment[..equalsIndex].Trim();
                modifierValue = modifierSegment[(equalsIndex + 1)..].Trim();
            }
            else
            {
                modifierOperator = modifierSegment;
            }
        }

        return !string.IsNullOrWhiteSpace(token);
    }

    private static bool TryParseBoolean(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }

        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            parsed = false;
            return true;
        }

        parsed = false;
        return false;
    }

    private static List<string> SplitCsv(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static string? GetRelationshipState(string npcName, RuntimeGameState state)
    {
        if (IsListed(state.Roommate, npcName))
        {
            return "Roommate";
        }

        if (IsListed(state.SpouseName, npcName)
            || IsListed(state.Spouse, npcName)
            || IsListed(state.MarriedTo, npcName)
            || IsListed(state.Spouses, npcName))
        {
            return "Married";
        }

        if (IsListed(state.EngagedTo, npcName))
        {
            return "Engaged";
        }

        if (state.DatingNpcNames.Contains(npcName))
        {
            return "Dating";
        }

        return state.FriendshipPoints.ContainsKey(npcName)
            ? "Friendly"
            : null;
    }

    private static bool IsListed(string? value, string expected)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsListed(IEnumerable<string>? values, string expected)
    {
        return values is not null
            && values.Any(value => string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase));
    }
}
