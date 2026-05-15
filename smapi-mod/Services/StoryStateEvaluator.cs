using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        TranslationCatalog? translationCatalog = null,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId = null)
    {
        var evaluations = nodes
            .Select(node =>
            {
                var conditionResult = this.conditionEvaluator.Evaluate(node.ConditionAst, state);
                var evaluatedNode = this.WithEvaluatedPatchWhenConditions(node, state, modConfigByUniqueId);
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
            UnknownConditions = BuildUnknownConditionReport(evaluations),
            Nodes = evaluations
        };
    }

    private static List<UnknownConditionSummary> BuildUnknownConditionReport(IEnumerable<StoryNodeEvaluation> evaluations)
    {
        var grouped = new Dictionary<string, UnknownConditionAccumulator>(StringComparer.Ordinal);

        foreach (var evaluation in evaluations)
        {
            if (evaluation.EventKind != StoryNodeEventKind.RegularLocationEvent
                || evaluation.Status is StoryNodeStatus.BranchTarget
                    or StoryNodeStatus.SpecialEvent
                    or StoryNodeStatus.NonTriggerable)
            {
                continue;
            }

            var seenRawForEvaluation = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in evaluation.UnknownFragments.Where(fragment => !string.IsNullOrWhiteSpace(fragment)))
            {
                AddUnknown(grouped, raw, evaluation, seenRawForEvaluation);
            }

            foreach (var atom in evaluation.ConditionResult.AtomResults.Where(atom => atom.Passed is null))
            {
                AddUnknown(grouped, atom.Raw ?? atom.AtomType ?? "Unknown", evaluation, seenRawForEvaluation);
            }

            foreach (var patchWhen in evaluation.PatchWhenConditions.Where(condition => !condition.IsKnown))
            {
                AddUnknown(grouped, patchWhen.Key, evaluation, seenRawForEvaluation);
            }
        }

        return grouped.Values
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Raw, StringComparer.Ordinal)
            .Select(item => new UnknownConditionSummary
            {
                Raw = item.Raw,
                Count = item.Count,
                SourceFiles = item.SourceFiles.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                ExampleEvents = item.ExampleEvents.Take(5).ToList(),
                SuggestedParserType = SuggestParserType(item.Raw)
            })
            .ToList();
    }

    private static void AddUnknown(
        IDictionary<string, UnknownConditionAccumulator> grouped,
        string raw,
        StoryNodeEvaluation evaluation,
        ISet<string> seenRawForEvaluation)
    {
        var key = string.IsNullOrWhiteSpace(raw) ? "Unknown" : raw.Trim();
        if (!seenRawForEvaluation.Add(key))
        {
            return;
        }

        if (!grouped.TryGetValue(key, out var accumulator))
        {
            accumulator = new UnknownConditionAccumulator(key);
            grouped[key] = accumulator;
        }

        accumulator.Count++;
        accumulator.ExampleEvents.Add(evaluation.EventId);
        foreach (var sourceFile in evaluation.EvidenceRefs
            .Select(refItem => refItem.SourcePath)
            .Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath)))
        {
            accumulator.SourceFiles.Add(sourceFile!);
        }
    }

    private static string SuggestParserType(string raw)
    {
        if (raw.Contains("{{", StringComparison.Ordinal))
        {
            return "dynamicToken";
        }

        if (raw.StartsWith("Query", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("PLAYER_", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("SEASON_DAY", StringComparison.OrdinalIgnoreCase))
        {
            return "gameStateQuery";
        }

        if (raw.Contains('|', StringComparison.Ordinal) || raw.Contains(':', StringComparison.Ordinal))
        {
            return "cpWhen";
        }

        return "eventKey";
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
            StoryNodeStatus.NonTriggerable => 5,
            StoryNodeStatus.BranchTarget => 6,
            StoryNodeStatus.SpecialEvent => 7,
            _ => int.MaxValue
        };
    }

    private sealed class UnknownConditionAccumulator
    {
        public UnknownConditionAccumulator(string raw)
        {
            this.Raw = raw;
        }

        public string Raw { get; }

        public int Count { get; set; }

        public HashSet<string> SourceFiles { get; } = new(StringComparer.Ordinal);

        public List<string> ExampleEvents { get; } = new();
    }

    private StoryNode WithEvaluatedPatchWhenConditions(
        StoryNode node,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        if (node.PatchWhenConditions.Count == 0)
        {
            return node;
        }

        return new StoryNode
        {
            NodeId = node.NodeId,
            EventId = node.EventId,
            EventKind = node.EventKind,
            SourceModId = node.SourceModId,
            SourceModName = node.SourceModName,
            AssetTarget = node.AssetTarget,
            Location = node.Location,
            RawKey = node.RawKey,
            RawPreconditions = node.RawPreconditions,
            PatchWhenConditions = this.EvaluatePatchWhenConditions(node, state, modConfigByUniqueId),
            ConditionAst = node.ConditionAst,
            UnknownFragments = node.UnknownFragments,
            RawScriptPreview = node.RawScriptPreview,
            EvidenceRefs = node.EvidenceRefs,
            SourceModConfigValues = node.SourceModConfigValues,
            SourceModDynamicTokens = node.SourceModDynamicTokens,
            RelatedDialogueRefs = node.RelatedDialogueRefs,
            RelatedEventChoiceRefs = node.RelatedEventChoiceRefs
        };
    }

    private List<PatchWhenCondition> EvaluatePatchWhenConditions(
        StoryNode node,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        var results = new List<PatchWhenCondition>(node.PatchWhenConditions.Count);
        foreach (var condition in node.PatchWhenConditions)
        {
            results.Add(this.EvaluatePatchWhenCondition(node, condition, state, modConfigByUniqueId));
        }

        return results;
    }

    private PatchWhenCondition EvaluatePatchWhenCondition(
        StoryNode node,
        PatchWhenCondition condition,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        condition = NormalizePatchWhenCondition(condition);

        if (TryClassifyKnownUnsupportedPatchWhen(condition, out var classifiedUnsupported))
        {
            return classifiedUnsupported;
        }

        if (TryEvaluateYearsMarriedCustomTokenPatchWhen(condition, out var yearsMarriedEvaluation))
        {
            return yearsMarriedEvaluation;
        }

        if (TryEvaluateCmctConfigPatchWhen(node, condition, modConfigByUniqueId, out var cmctEvaluation))
        {
            return cmctEvaluation;
        }

        if (TryEvaluateRelationshipCondition(condition, state, out var relationshipEvaluation))
        {
            return relationshipEvaluation;
        }

        if (TryEvaluateSpouseCondition(condition, state, out var spouseEvaluation))
        {
            return spouseEvaluation;
        }

        if (TryEvaluateHeartsCondition(condition, state, out var heartsEvaluation))
        {
            return heartsEvaluation;
        }

        if (TryEvaluateHasModCondition(condition, state, out var hasModEvaluation))
        {
            return hasModEvaluation;
        }

        if (TryEvaluateHasSeenEventCondition(condition, state, out var hasSeenEventEvaluation))
        {
            return hasSeenEventEvaluation;
        }

        if (TryEvaluateHasConversationTopicCondition(condition, state, out var conversationTopicEvaluation))
        {
            return conversationTopicEvaluation;
        }

        if (TryEvaluateHasFlagCondition(condition, state, out var hasFlagEvaluation))
        {
            return hasFlagEvaluation;
        }

        if (TryEvaluateSeasonCondition(condition, state, out var seasonEvaluation))
        {
            return seasonEvaluation;
        }

        if (TryEvaluateLocationNameCondition(condition, state, out var locationEvaluation))
        {
            return locationEvaluation;
        }

        if (TryEvaluateDayEventCondition(condition, state, out var dayEventEvaluation))
        {
            return dayEventEvaluation;
        }

        if (TryEvaluateFarmhouseUpgradePatchWhen(condition, state, out var farmhouseEvaluation))
        {
            return farmhouseEvaluation;
        }

        if (TryEvaluateConfigCondition(node, condition, out var configEvaluation))
        {
            return configEvaluation;
        }

        if (TryEvaluateDynamicTokenValueCondition(node, condition, state, modConfigByUniqueId, out var dynamicTokenEvaluation))
        {
            return dynamicTokenEvaluation;
        }

        if (TryEvaluateDatePatchWhen(node, condition, modConfigByUniqueId, out var dateEvaluation))
        {
            return dateEvaluation;
        }

        if (TryEvaluateFarmerCheaterCondition(node, condition, state, out var farmerCheaterEvaluation))
        {
            return farmerCheaterEvaluation;
        }

        if (TryEvaluateSimpleQueryCondition(node, condition, state, modConfigByUniqueId, out var queryEvaluation))
        {
            return queryEvaluation;
        }

        return CreateUnknownPatchWhenCondition(
            condition,
            LooksLikeComplexOrRandomPatchWhen(condition)
                ? "Complex CP Query is not expanded."
                : $"Patch-level When condition '{condition.Key}' is not evaluated.",
            unknownKind: LooksLikeComplexOrRandomPatchWhen(condition) ? "complexQueryUnsupported" : "parseUnknown",
            reasonZh: LooksLikeComplexOrRandomPatchWhen(condition)
                ? "随机/概率条件暂不展开。"
                : $"未解析条件：{condition.Key}");
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
            var expectedValues = SplitCsv(modifierValue);
            var contains = expectedValues.Any(value => string.Equals(currentLabel, value, StringComparison.OrdinalIgnoreCase));
            passed = contains == wantContains;
            expectation = wantContains
                ? $"relationship contains [{string.Join(", ", expectedValues)}]"
                : $"relationship does not contain [{string.Join(", ", expectedValues)}]";
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
            $"Relationship failed: {argument} is {currentLabel}, expected {expectation}.",
            isProgressionSensitive: true);
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
            $"Hearts failed: {argument} has {currentHearts} hearts, requires exactly {requiredHearts}.",
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateSpouseCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "Spouse", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(modifierOperator, "contains", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(modifierValue))
        {
            return false;
        }

        var expectedSpouse = TryParseBoolean(condition.Value, out var parsedBool) ? parsedBool : true;
        var candidates = SplitCsv(modifierValue);
        var matched = candidates
            .Where(candidate => IsListed(state.SpouseName, candidate)
                || IsListed(state.Spouse, candidate)
                || IsListed(state.MarriedTo, candidate)
                || IsListed(state.Spouses, candidate)
                || IsListed(state.EngagedTo, candidate))
            .ToList();
        var hasAny = matched.Count > 0;
        var passed = hasAny == expectedSpouse;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            hasAny
                ? $"Spouse matched: found [{string.Join(", ", matched)}]."
                : "Spouse matched: none of the candidate spouses are present.",
            hasAny
                ? $"Spouse failed: found [{string.Join(", ", matched)}]."
                : $"Spouse failed: none of [{string.Join(", ", candidates)}] are present.",
            isProgressionSensitive: true);
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
            || !TryReadContainsExpectation(condition, modifierOperator, modifierValue, out var candidateModIds, out var expectedInstalled)
            || candidateModIds.Count == 0)
        {
            return false;
        }

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
                : $"HasMod failed: none of [{string.Join(", ", candidateModIds)}] are installed.",
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateHasSeenEventCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "HasSeenEvent", StringComparison.OrdinalIgnoreCase)
            || !TryReadContainsExpectation(condition, modifierOperator, modifierValue, out var candidateEventIds, out var expectedSeen)
            || candidateEventIds.Count == 0)
        {
            return false;
        }

        var matchedEventIds = candidateEventIds
            .Where(candidate => state.SeenEvents.Contains(candidate))
            .ToList();
        var hasAny = matchedEventIds.Count > 0;
        var passed = hasAny == expectedSeen;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            hasAny
                ? $"HasSeenEvent matched: seen [{string.Join(", ", matchedEventIds)}]."
                : "HasSeenEvent matched: none of the candidate events have been seen.",
            hasAny
                ? $"HasSeenEvent failed: seen [{string.Join(", ", matchedEventIds)}]."
                : $"HasSeenEvent failed: none of [{string.Join(", ", candidateEventIds)}] have been seen.",
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateHasConversationTopicCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "HasConversationTopic", StringComparison.OrdinalIgnoreCase)
            || !TryReadContainsExpectation(condition, modifierOperator, modifierValue, out var topics, out var expectedHasTopic)
            || topics.Count == 0)
        {
            return false;
        }

        var matchedTopics = topics.Where(topic => state.DialogueAnswers.Contains(topic)).ToList();
        var hasAny = matchedTopics.Count > 0;
        var passed = hasAny == expectedHasTopic;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            hasAny
                ? $"HasConversationTopic matched: found [{string.Join(", ", matchedTopics)}]."
                : "HasConversationTopic matched: none of the candidate topics are active/recorded.",
            hasAny
                ? $"HasConversationTopic failed: found [{string.Join(", ", matchedTopics)}]."
                : $"HasConversationTopic failed: none of [{string.Join(", ", topics)}] are active/recorded.",
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateHasFlagCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOperator, out var modifierValue)
            || !string.Equals(token, "HasFlag", StringComparison.OrdinalIgnoreCase)
            || !TryReadContainsExpectation(condition, modifierOperator, modifierValue, out var flags, out var expectedHasFlag)
            || flags.Count == 0)
        {
            return false;
        }

        var matchedFlags = flags.Where(flag => state.Mail.Contains(flag)).ToList();
        var hasAny = matchedFlags.Count > 0;
        var passed = hasAny == expectedHasFlag;
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            hasAny
                ? $"HasFlag matched: found [{string.Join(", ", matchedFlags)}]."
                : "HasFlag matched: none of the candidate flags are present.",
            hasAny
                ? $"HasFlag failed: found [{string.Join(", ", matchedFlags)}]."
                : $"HasFlag failed: none of [{string.Join(", ", flags)}] are present.",
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateSeasonCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out _, out _)
            || !string.Equals(token, "Season", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedSeasons = SplitCsv(condition.Value);
        if (expectedSeasons.Count == 0)
        {
            return false;
        }

        var passed = expectedSeasons.Any(value => string.Equals(value, state.Season, StringComparison.OrdinalIgnoreCase));
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"Season matched: current season is {state.Season}.",
            $"Season failed: current season is {state.Season}, expected one of [{string.Join(", ", expectedSeasons)}].",
            isContextSensitive: true);
        return true;
    }

    private static bool TryEvaluateLocationNameCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out _, out _)
            || !string.Equals(token, "LocationName", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(condition.Value))
        {
            return false;
        }

        var expectedLocations = SplitCsv(condition.Value);
        if (expectedLocations.Count == 0)
        {
            return false;
        }

        var passed = expectedLocations.Any(value => string.Equals(value, state.CurrentLocation, StringComparison.OrdinalIgnoreCase));
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"LocationName matched: current location is {state.CurrentLocation}.",
            $"LocationName failed: current location is {state.CurrentLocation}, expected one of [{string.Join(", ", expectedLocations)}].",
            isContextSensitive: true);
        return true;
    }

    private static bool TryEvaluateDayEventCondition(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modifierOp, out var modifierVal)
            || !string.Equals(token, "DayEvent", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(modifierOp, "contains", StringComparison.OrdinalIgnoreCase)
            && TryReadContainsExpectation(condition, modifierOp, modifierVal, out var festivalCandidates, out var expectedListMatch))
        {
            if (!state.DayEventsKnown)
            {
                evaluated = CreateUnknownPatchWhenCondition(
                    condition,
                    $"DayEvent |contains runtime export is unavailable.",
                    unknownKind: "runtimeMissing",
                    reasonZh: "需要判断当前节日/特殊日是否在列表中，但运行时未导出 DayEvent 状态。",
                    parsedType: "cpDayEvent");
                return true;
            }

            static bool DayEventLabelMatches(string current, string candidate)
            {
                if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return current.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains(current, StringComparison.OrdinalIgnoreCase);
            }

            var containsResult = festivalCandidates.Any(candidate =>
                state.DayEvents.Any(dayEvent => DayEventLabelMatches(dayEvent, candidate)));

            var listPassed = containsResult == expectedListMatch;
            var listZh = string.Join("、", festivalCandidates);
            var reasonZh = expectedListMatch
                ? (listPassed
                    ? $"今天需要是以下节日/特殊日之一（已满足）：{listZh}"
                    : $"今天需要是以下节日/特殊日之一：{listZh}")
                : (listPassed
                    ? $"今天不能是以下节日/特殊日（已满足）：{listZh}"
                    : $"今天不能是以下节日/特殊日：{listZh}");

            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                listPassed,
                $"DayEvent contains matched: list intersection is {containsResult}, expected {expectedListMatch}.",
                $"DayEvent contains failed: list intersection is {containsResult}, expected {expectedListMatch}.",
                isContextSensitive: true,
                isProgressionSensitive: false,
                reasonZh: reasonZh);
            return true;
        }

        var expected = condition.Value.Trim();

        if (TryParseBoolean(expected, out var expectedDayEvent))
        {
            if (state.IsFestivalDay is null)
            {
                return false;
            }

            var boolPassed = state.IsFestivalDay.Value == expectedDayEvent;
            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                boolPassed,
                $"DayEvent matched: festival/day-event state is {state.IsFestivalDay.Value}.",
                $"DayEvent failed: festival/day-event state is {state.IsFestivalDay.Value}, expected {expectedDayEvent}.",
                isContextSensitive: true);
            return true;
        }

        if (!state.DayEventsKnown)
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                $"DayEvent runtime export is unavailable for '{expected}'.",
                unknownKind: "runtimeMissing",
                reasonZh: $"需要 DayEvent={expected}，但当前运行时未导出 DayEvent 状态。",
                parsedType: "cpDayEvent");
            return true;
        }

        var passed = state.DayEvents.Any(dayEvent =>
            string.Equals(dayEvent, expected, StringComparison.OrdinalIgnoreCase));

        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"DayEvent matched: current day event includes {expected}.",
            $"DayEvent failed: current day events are [{string.Join(", ", state.DayEvents)}], expected {expected}.",
            isContextSensitive: true,
            reasonZh: passed
                ? $"今日事件满足：包含 {expected}"
                : $"今日事件不满足：当前为 [{string.Join(", ", state.DayEvents)}]，需要 {expected}");

        return true;
    }


    private static bool TryEvaluateFarmhouseUpgradePatchWhen(
        PatchWhenCondition condition,
        RuntimeGameState state,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out _, out var modOp, out var modVal)
            || !string.Equals(token, "FarmhouseUpgrade", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!state.FarmhouseUpgradeKnown)
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "FarmhouseUpgrade runtime export is unavailable.",
                unknownKind: "runtimeMissing",
                reasonZh: "当前暂无法判断农舍升级等级。",
                parsedType: "cpFarmhouseUpgrade");
            return true;
        }

        var level = state.FarmhouseUpgradeLevel ?? 0;

        if (string.IsNullOrWhiteSpace(modOp))
        {
            if (!int.TryParse(condition.Value.Trim(), out var wantLevel))
            {
                return false;
            }

            var passedExact = level == wantLevel;
            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                passedExact,
                $"FarmhouseUpgrade matched: level is {level}.",
                $"FarmhouseUpgrade failed: level is {level}, expected {wantLevel}.",
                isContextSensitive: false,
                isProgressionSensitive: true);
            return true;
        }

        if (string.Equals(modOp, "contains", StringComparison.OrdinalIgnoreCase)
            && TryReadContainsExpectation(condition, modOp, modVal, out var parts, out var expectedBool))
        {
            var levels = new List<int>();
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out var parsedLevel))
                {
                    levels.Add(parsedLevel);
                }
            }

            if (levels.Count == 0)
            {
                return false;
            }

            var contains = levels.Contains(level);
            var passed = contains == expectedBool;
            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                passed,
                $"FarmhouseUpgrade contains matched: level {level} in set [{string.Join(", ", levels)}] is {contains}.",
                $"FarmhouseUpgrade contains failed: level {level} versus set [{string.Join(", ", levels)}].",
                isContextSensitive: false,
                isProgressionSensitive: true);
            return true;
        }

        return false;
    }

    private static bool TryEvaluateYearsMarriedCustomTokenPatchWhen(
        PatchWhenCondition condition,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;
        if (!condition.Key.Contains("YearsMarried", StringComparison.OrdinalIgnoreCase)
            || !condition.Key.Contains("CustomTokens", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        evaluated = CreateUnknownPatchWhenCondition(
            condition,
            "External CustomTokens YearsMarried is not exported at patch-eval time.",
            unknownKind: "externalTokenMissing",
            reasonZh: "外部 CustomTokens 年限未导出。",
            parsedType: "externalCustomToken");
        return true;
    }

    private static bool TryEvaluateCmctConfigPatchWhen(
        StoryNode node,
        PatchWhenCondition condition,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!TryParseTokenAndModifier(condition.Key, out var token, out var argument, out var modOp, out var modVal)
            || !string.Equals(token, "Spiderbuttons.CMCT/Config", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        var commaIdx = argument.IndexOf(',');
        if (commaIdx <= 0 || commaIdx >= argument.Length - 1)
        {
            return false;
        }

        var targetModId = argument[..commaIdx].Trim();
        var configKey = argument[(commaIdx + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(targetModId) || string.IsNullOrWhiteSpace(configKey))
        {
            return false;
        }

        if (modConfigByUniqueId is null
            || !modConfigByUniqueId.TryGetValue(targetModId, out var cfg)
            || !cfg.TryGetValue(configKey, out var actual)
            || string.IsNullOrWhiteSpace(actual))
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                $"CMCT target config not found for mod '{targetModId}', key '{configKey}'.",
                unknownKind: "externalTokenMissing",
                reasonZh: $"找不到 mod「{targetModId}」的配置项「{configKey}」。",
                parsedType: "cmctConfig");
            return true;
        }

        actual = actual.Trim();

        if (string.Equals(modOp, "contains", StringComparison.OrdinalIgnoreCase))
        {
            var needle = (modVal ?? string.Empty).Trim();
            var wantContains = TryParseBoolean(condition.Value, out var parsedExpected) ? parsedExpected : true;
            var haystack = actual;
            var contains = haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
            var passed = contains == wantContains;
            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                passed,
                $"CMCT Config matched: {targetModId}/{configKey}={actual}.",
                $"CMCT Config failed: {targetModId}/{configKey}={actual}, expected contains {needle} ({wantContains}).",
                isContextSensitive: false,
                isProgressionSensitive: true);
            return true;
        }

        var expectedValues = SplitCsv(condition.Value);
        if (expectedValues.Count == 0)
        {
            expectedValues.Add(condition.Value.Trim());
        }

        var passedExact = expectedValues.Any(expected =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passedExact,
            $"CMCT Config matched: {targetModId}/{configKey}={actual}.",
            $"CMCT Config failed: {targetModId}/{configKey}={actual}, expected {condition.Value}.",
            isContextSensitive: false,
            isProgressionSensitive: true);
        return true;
    }

    private static bool TryEvaluateDatePatchWhen(
        StoryNode node,
        PatchWhenCondition condition,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!string.Equals(condition.Key.Trim(), "Date", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(condition.Value))
        {
            return false;
        }

        var resolved = TryResolvePatchWhenDateValue(node, modConfigByUniqueId);
        if (resolved is null)
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "Date CP When token is not exported from config or dynamic tokens.",
                unknownKind: "externalTokenMissing",
                reasonZh: "CP When Date 未能从本包配置、动态 token 或其他 mod 配置中解析。",
                parsedType: "cpDate");
            return true;
        }

        var passed = string.Equals(resolved.Trim(), condition.Value.Trim(), StringComparison.OrdinalIgnoreCase);
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"Date matched: resolved '{resolved}'.",
            $"Date failed: resolved '{resolved}', expected '{condition.Value.Trim()}'.",
            isProgressionSensitive: true,
            reasonZh: passed
                ? $"日期条件满足：{resolved}"
                : $"日期条件不满足：当前解析为 {resolved}，需要 {condition.Value.Trim()}");
        return true;
    }

    private static string? TryResolvePatchWhenDateValue(
        StoryNode node,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        if (node.SourceModConfigValues.TryGetValue("Date", out var d0) && !string.IsNullOrWhiteSpace(d0))
        {
            return d0.Trim();
        }

        if (node.SourceModDynamicTokens.TryGetValue("Date", out var defs))
        {
            foreach (var def in defs)
            {
                var v = def.Value?.Trim() ?? string.Empty;
                if (v.Length > 0 && def.WhenConditions.Count == 0)
                {
                    return v;
                }
            }
        }

        if (modConfigByUniqueId is not null)
        {
            foreach (var pair in modConfigByUniqueId)
            {
                if (pair.Value.TryGetValue("Date", out var d1) && !string.IsNullOrWhiteSpace(d1))
                {
                    return d1.Trim();
                }
            }
        }

        return null;
    }

    private static bool TryEvaluateConfigCondition(
        StoryNode node,
        PatchWhenCondition condition,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        var key = condition.Key.Trim();
        if (string.IsNullOrWhiteSpace(key)
            || node.SourceModConfigValues.Count == 0
            || !TryResolveConfigValue(node, key, out var resolvedKey, out var actual))
        {
            return false;
        }

        key = resolvedKey;

        var expectedValues = SplitCsv(condition.Value);
        if (expectedValues.Count == 0)
        {
            expectedValues.Add(condition.Value.Trim());
        }

        var passed = expectedValues.Any(expected =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));

        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"Config matched: {key}={actual}.",
            $"Config failed: {key}={actual}, expected {condition.Value}.",
            isContextSensitive: false,
            isProgressionSensitive: false);

        return true;
    }

    private bool TryEvaluateDynamicTokenValueCondition(
        StoryNode node,
        PatchWhenCondition condition,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        var tokenName = condition.Key.Trim();
        if (string.IsNullOrWhiteSpace(tokenName)
            || tokenName.Contains('|', StringComparison.Ordinal)
            || string.Equals(tokenName, "Query", StringComparison.OrdinalIgnoreCase)
            || tokenName.StartsWith("Query:", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tokenName, "Date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tokenName, "FarmhouseUpgrade", StringComparison.OrdinalIgnoreCase)
            || tokenName.StartsWith("Spiderbuttons.CMCT/Config", StringComparison.OrdinalIgnoreCase)
            || tokenName.StartsWith("Pregnant", StringComparison.OrdinalIgnoreCase)
            || tokenName.StartsWith("HavingChild", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!node.SourceModDynamicTokens.TryGetValue(tokenName, out var definitions) || definitions.Count == 0)
        {
            return false;
        }

        var expectedValue = condition.Value.Trim();
        var resolution = this.ResolveDynamicTokenValue(node, definitions, state, modConfigByUniqueId);
        if (resolution.HasUnknown)
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                $"DynamicToken '{tokenName}' guard conditions could not be fully evaluated.",
                unknownKind: resolution.UnknownKind ?? "complexQueryUnsupported",
                reasonZh: resolution.ReasonZh ?? $"DynamicToken「{tokenName}」的守卫条件无法完全评估。");
            return true;
        }

        if (resolution.ResolvedValue is null)
        {
            evaluated = CreateEvaluatedPatchWhenCondition(
                condition,
                false,
                $"DynamicToken matched: '{tokenName}' has no active branch.",
                $"DynamicToken failed: '{tokenName}' has no active branch, expected '{expectedValue}'.",
                isContextSensitive: false,
                isProgressionSensitive: true);
            return true;
        }

        var passed = string.Equals(resolution.ResolvedValue, expectedValue, StringComparison.OrdinalIgnoreCase);
        evaluated = CreateEvaluatedPatchWhenCondition(
            condition,
            passed,
            $"DynamicToken matched: '{tokenName}' resolves to '{resolution.ResolvedValue}'.",
            $"DynamicToken failed: '{tokenName}' resolves to '{resolution.ResolvedValue}', expected '{expectedValue}'.",
            isContextSensitive: false,
            isProgressionSensitive: true);
        return true;
    }

    private sealed class DynamicTokenResolution
    {
        public string? ResolvedValue { get; init; }

        public bool HasUnknown { get; init; }

        public string? UnknownKind { get; init; }

        public string? ReasonZh { get; init; }
    }

    private DynamicTokenResolution ResolveDynamicTokenValue(
        StoryNode node,
        IReadOnlyList<DynamicTokenDefinition> definitions,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        string? resolvedValue = null;
        var hasUnknown = false;
        string? unknownKind = null;
        string? reasonZh = null;

        foreach (var definition in definitions)
        {
            var branchResult = this.EvaluateDynamicTokenBranch(node, definition, state, modConfigByUniqueId);
            if (branchResult.HasUnknown)
            {
                hasUnknown = true;
                unknownKind = branchResult.UnknownKind ?? unknownKind;
                reasonZh = branchResult.ReasonZh ?? reasonZh;
                continue;
            }

            if (branchResult.Passed == true)
            {
                resolvedValue = definition.Value;
                break;
            }
        }

        return new DynamicTokenResolution
        {
            ResolvedValue = resolvedValue,
            HasUnknown = hasUnknown,
            UnknownKind = unknownKind,
            ReasonZh = reasonZh
        };
    }

    private sealed class BranchEvaluation
    {
        public bool? Passed { get; init; }

        public bool HasUnknown { get; init; }

        public string? UnknownKind { get; init; }

        public string? ReasonZh { get; init; }
    }

    private BranchEvaluation EvaluateDynamicTokenBranch(
        StoryNode node,
        DynamicTokenDefinition definition,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId)
    {
        if (definition.WhenConditions.Count == 0)
        {
            return new BranchEvaluation { Passed = true, HasUnknown = false };
        }

        var hasUnknown = false;
        var hasFalse = false;
        string? unknownKind = null;
        string? reasonZh = null;
        foreach (var guard in definition.WhenConditions)
        {
            var evaluated = this.EvaluatePatchWhenCondition(node, NormalizePatchWhenCondition(guard), state, modConfigByUniqueId);
            if (!evaluated.IsKnown)
            {
                hasUnknown = true;
                unknownKind = evaluated.UnknownKind ?? unknownKind;
                reasonZh = evaluated.ReasonZh ?? reasonZh;
                continue;
            }

            if (evaluated.Passed == false)
            {
                hasFalse = true;
            }
        }

        if (hasFalse)
        {
            return new BranchEvaluation
            {
                Passed = false,
                HasUnknown = hasUnknown,
                UnknownKind = unknownKind,
                ReasonZh = reasonZh
            };
        }

        if (hasUnknown)
        {
            return new BranchEvaluation
            {
                Passed = null,
                HasUnknown = true,
                UnknownKind = unknownKind,
                ReasonZh = reasonZh
            };
        }

        return new BranchEvaluation { Passed = true, HasUnknown = false };
    }

    private bool TryEvaluateSimpleQueryCondition(
        StoryNode node,
        PatchWhenCondition condition,
        RuntimeGameState state,
        IReadOnlyDictionary<string, Dictionary<string, string>>? modConfigByUniqueId,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;

        if (!string.Equals(condition.Key.Trim(), "Query", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseSimpleQueryExpression(condition.Value, out var clauseGroups, out var usesOr))
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "Complex CP Query is not expanded.",
                unknownKind: "complexQueryUnsupported",
                reasonZh: "随机/概率条件暂不展开。");
            return true;
        }

        var groupResults = new List<PatchWhenCondition>();
        foreach (var clauseGroup in clauseGroups)
        {
            var clauseResults = new List<PatchWhenCondition>();
            foreach (var clause in clauseGroup)
            {
                clauseResults.Add(this.EvaluatePatchWhenCondition(node, clause, state, modConfigByUniqueId));
            }

            var groupPassed = clauseResults.All(result => result.IsKnown && result.Passed == true);
            var groupFailed = clauseResults.Any(result => result.IsKnown && result.Passed == false);
            var groupUnknown = clauseResults.Any(result => !result.IsKnown);

            groupResults.Add(
                groupUnknown
                    ? CreateUnknownPatchWhenCondition(
                        condition,
                        "Query clause could not be fully evaluated.",
                        unknownKind: "complexQueryUnsupported",
                        reasonZh: "随机/概率条件暂不展开。")
                    : CreateEvaluatedPatchWhenCondition(
                        condition,
                        groupPassed,
                        "Query clause group passed.",
                        "Query clause group failed.",
                        isContextSensitive: false,
                        isProgressionSensitive: true));
        }

        if (usesOr)
        {
            if (groupResults.Any(result => result.IsKnown && result.Passed == true))
            {
                evaluated = CreateEvaluatedPatchWhenCondition(
                    condition,
                    true,
                    "Query OR matched: at least one clause group passed.",
                    "Query OR failed.",
                    isContextSensitive: false,
                    isProgressionSensitive: true);
                return true;
            }

            if (groupResults.All(result => result.IsKnown && result.Passed == false))
            {
                evaluated = CreateEvaluatedPatchWhenCondition(
                    condition,
                    false,
                    "Query OR matched: none passed.",
                    "Query OR failed: every clause group failed.",
                    isContextSensitive: false,
                    isProgressionSensitive: true);
                return true;
            }

            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "Complex CP Query is not expanded.",
                unknownKind: "complexQueryUnsupported",
                reasonZh: "随机/概率条件暂不展开。");
            return true;
        }

        evaluated = groupResults[0];
        return true;
    }

    private static bool TryParseSimpleQueryExpression(
        string expression,
        out List<List<PatchWhenCondition>> clauseGroups,
        out bool usesOr)
    {
        clauseGroups = new List<List<PatchWhenCondition>>();
        usesOr = false;

        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var topLevelParts = SplitTopLevel(trimmed, " OR ");
        usesOr = topLevelParts.Count > 1;

        foreach (var orPart in topLevelParts)
        {
            var andParts = SplitTopLevel(orPart, " AND ");
            var clauses = new List<PatchWhenCondition>();
            foreach (var andPart in andParts)
            {
                if (!TryParseQueryClause(andPart, out var clause))
                {
                    return false;
                }

                clauses.Add(clause);
            }

            if (clauses.Count == 0)
            {
                return false;
            }

            clauseGroups.Add(clauses);
        }

        return clauseGroups.Count > 0;
    }

    private static bool TryParseQueryClause(string clause, out PatchWhenCondition condition)
    {
        condition = new PatchWhenCondition();

        var trimmed = clause.Trim();
        if (trimmed.Contains(">=", StringComparison.Ordinal)
            || trimmed.Contains("<=", StringComparison.Ordinal)
            || trimmed.Contains("!=", StringComparison.Ordinal)
            || trimmed.Contains("<>", StringComparison.Ordinal))
        {
            return false;
        }

        var match = Regex.Match(
            trimmed,
            @"^'?\{\{([^}]+)\}\}'?\s*=\s*'([^']*)'$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        condition = new PatchWhenCondition
        {
            Key = match.Groups[1].Value.Trim(),
            Value = match.Groups[2].Value.Trim(),
            RawValue = trimmed,
            IsKnown = false,
            Reason = "Query clause pending evaluation."
        };
        return true;
    }

    private static List<string> SplitTopLevel(string expression, string separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var inQuotes = false;
        var separatorUpper = separator.Trim().ToUpperInvariant();

        for (var index = 0; index < expression.Length; index++)
        {
            var ch = expression[index];
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes && ch == '(')
            {
                depth++;
                current.Append(ch);
                continue;
            }

            if (!inQuotes && ch == ')')
            {
                depth = Math.Max(0, depth - 1);
                current.Append(ch);
                continue;
            }

            if (!inQuotes
                && depth == 0
                && index + separator.Length <= expression.Length
                && string.Equals(
                    expression.Substring(index, separator.Length),
                    separator,
                    StringComparison.OrdinalIgnoreCase))
            {
                var piece = current.ToString().Trim();
                if (piece.Length > 0)
                {
                    parts.Add(piece);
                }

                current.Clear();
                index += separator.Length - 1;
                continue;
            }

            current.Append(ch);
        }

        var tail = current.ToString().Trim();
        if (tail.Length > 0)
        {
            parts.Add(tail);
        }

        return parts.Count > 0 ? parts : new List<string> { expression.Trim() };
    }

    private static bool LooksLikeComplexQuery(PatchWhenCondition condition)
    {
        var key = condition.Key.Trim();
        return string.Equals(key, "Query", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("Query:", StringComparison.OrdinalIgnoreCase)
            || condition.Value.Contains(" OR ", StringComparison.OrdinalIgnoreCase)
            || condition.Value.Contains(" AND ", StringComparison.OrdinalIgnoreCase)
            || condition.Value.Contains("{{", StringComparison.Ordinal)
            || key.Contains(">=", StringComparison.Ordinal)
            || key.Contains("<=", StringComparison.Ordinal);
    }

    private static bool LooksLikeComplexOrRandomPatchWhen(PatchWhenCondition condition)
    {
        return LooksLikeComplexQuery(condition)
            || condition.Value.Contains(">=", StringComparison.Ordinal)
            || condition.Value.Contains("<=", StringComparison.Ordinal)
            || condition.Key.Contains("drinkchance", StringComparison.OrdinalIgnoreCase);
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
                : "FarmerCheater failed: no supported polyamory mods detected.",
            isProgressionSensitive: true);
        return true;
    }

    private static PatchWhenCondition CreateEvaluatedPatchWhenCondition(
        PatchWhenCondition original,
        bool passed,
        string successReason,
        string failureReason,
        bool isProgressionSensitive = false,
        bool isContextSensitive = false,
        string reasonZh = "")
    {
        return new PatchWhenCondition
        {
            Key = original.Key,
            Value = original.Value,
            RawValue = original.RawValue,
            IsKnown = true,
            Passed = passed,
            IsContextSensitive = isContextSensitive,
            IsProgressionSensitive = isProgressionSensitive,
            Reason = passed ? successReason : failureReason,
            ReasonZh = string.IsNullOrWhiteSpace(reasonZh)
                ? (passed ? successReason : failureReason)
                : reasonZh
        };
    }

    private static PatchWhenCondition CreateUnknownPatchWhenCondition(
        PatchWhenCondition original,
        string reason,
        string unknownKind = "parseUnknown",
        string reasonZh = "",
        string parsedType = "")
    {
        return new PatchWhenCondition
        {
            Key = original.Key,
            Value = original.Value,
            RawValue = original.RawValue,
            IsKnown = false,
            Passed = null,
            IsContextSensitive = original.IsContextSensitive,
            IsProgressionSensitive = original.IsProgressionSensitive,
            Reason = reason,
            UnknownKind = unknownKind,
            ReasonZh = string.IsNullOrWhiteSpace(reasonZh) ? reason : reasonZh,
            ParsedType = parsedType
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

    private static bool TryReadContainsExpectation(
        PatchWhenCondition condition,
        string modifierOperator,
        string modifierValue,
        out List<string> candidates,
        out bool expected)
    {
        candidates = new List<string>();
        expected = true;

        if (string.Equals(modifierOperator, "contains", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(modifierValue))
        {
            candidates = SplitCsv(modifierValue);
            if (TryParseBoolean(condition.Value, out var parsedExpected))
            {
                expected = parsedExpected;
            }

            return candidates.Count > 0;
        }

        if (TryParseBoolean(condition.Value, out var onlyBoolean))
        {
            expected = onlyBoolean;
            return false;
        }

        candidates = SplitCsv(condition.Value);
        return candidates.Count > 0;
    }

    private static List<string> SplitCsv(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (parsed is not null)
                {
                    return parsed
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .ToList();
                }
            }
            catch
            {
                // Fall through to permissive comma splitting for loose values.
            }
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim().Trim('"'))
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

    private static PatchWhenCondition NormalizePatchWhenCondition(PatchWhenCondition condition)
    {
        var key = condition.Key?.Trim() ?? string.Empty;
        if (key.StartsWith("Query:", StringComparison.OrdinalIgnoreCase))
        {
            var expression = key["Query:".Length..].Trim();
            if (expression.Length > 0)
            {
                return new PatchWhenCondition
                {
                    Key = "Query",
                    Value = expression,
                    RawValue = condition.RawValue,
                    IsKnown = condition.IsKnown,
                    Passed = condition.Passed,
                    Reason = condition.Reason,
                    ReasonZh = condition.ReasonZh,
                    UnknownKind = condition.UnknownKind,
                    ParsedType = condition.ParsedType,
                    IsContextSensitive = condition.IsContextSensitive,
                    IsProgressionSensitive = condition.IsProgressionSensitive
                };
            }
        }

        return condition;
    }

    private static bool TryClassifyKnownUnsupportedPatchWhen(
        PatchWhenCondition condition,
        out PatchWhenCondition evaluated)
    {
        evaluated = condition;
        var key = condition.Key.Trim();
        var lowerKey = key.ToLowerInvariant();

        if (lowerKey.StartsWith("pregnant", StringComparison.Ordinal))
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "Pregnant CP When requires runtime family state that is not exported.",
                unknownKind: "runtimeMissing",
                reasonZh: "无法判断：运行时家庭状态未导出（Pregnant）。",
                parsedType: "cpFamilyState");
            return true;
        }

        if (lowerKey.StartsWith("havingchild", StringComparison.Ordinal))
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "HavingChild CP When requires runtime family state that is not exported.",
                unknownKind: "runtimeMissing",
                reasonZh: "无法判断：运行时家庭状态未导出（HavingChild）。",
                parsedType: "cpFamilyState");
            return true;
        }

        if (lowerKey.Contains("drinkchance", StringComparison.Ordinal)
            || (lowerKey.StartsWith("query:", StringComparison.Ordinal) && condition.Value.Contains("<=", StringComparison.Ordinal)))
        {
            evaluated = CreateUnknownPatchWhenCondition(
                condition,
                "Random/probability CP Query is not expanded.",
                unknownKind: "randomTokenUnsupported",
                reasonZh: "随机/概率条件暂不展开。",
                parsedType: "cpRandomQuery");
            return true;
        }

        return false;
    }

    private static bool TryResolveConfigValue(
        StoryNode node,
        string rawKey,
        out string resolvedKey,
        out string actual)
    {
        resolvedKey = rawKey.Trim();
        actual = string.Empty;

        if (node.SourceModConfigValues.TryGetValue(resolvedKey, out var direct))
        {
            actual = direct;
            return true;
        }

        var slashIndex = resolvedKey.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < resolvedKey.Length - 1)
        {
            var tail = resolvedKey[(slashIndex + 1)..].Trim();
            if (node.SourceModConfigValues.TryGetValue(tail, out var tailValue))
            {
                resolvedKey = tail;
                actual = tailValue;
                return true;
            }
        }

        return false;
    }
}
