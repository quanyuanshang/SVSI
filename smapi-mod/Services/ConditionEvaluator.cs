using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class ConditionEvaluator
{
    private static readonly HashSet<string> ContextSensitiveAtomTypes = new(StringComparer.Ordinal)
    {
        "Season",
        "DayOfMonth",
        "Day",
        "DayOfWeek",
        "Time",
        "Weather",
        "FestivalDay",
        "Location",
        "NpcVisibleHere",
        "InUpgradedHouse"
    };

    private static readonly HashSet<string> ProgressionSensitiveAtomTypes = new(StringComparer.Ordinal)
    {
        "Friendship",
        "Dating",
        "Relationship",
        "SawEvent",
        "Mail",
        "LocalMail",
        "HostMail",
        "HostOrLocalMail",
        "ChoseDialogueAnswers",
        "ActiveDialogueEvent",
        "Spouse"
    };

    public ConditionEvaluationResult Evaluate(ConditionAstNode ast, RuntimeGameState state)
    {
        return this.EvaluateNode(ast, state);
    }

    private ConditionEvaluationResult EvaluateNode(ConditionAstNode? node, RuntimeGameState state)
    {
        if (node is null)
        {
            return new ConditionEvaluationResult
            {
                Passed = null,
                HasUnknown = true,
                Reason = "Condition node is missing."
            };
        }

        return node.Type switch
        {
            "AllOf" => this.EvaluateAllOf(node, state),
            "AnyOf" => this.EvaluateAnyOf(node, state),
            "Not" => this.EvaluateNot(node, state),
            "Atom" => this.EvaluateAtom(node, state),
            "Unknown" => this.EvaluateUnknown(node),
            _ => new ConditionEvaluationResult
            {
                Passed = null,
                HasUnknown = true,
                Reason = $"Unsupported condition node type: {node.Type}",
                AtomResults =
                {
                    this.CreateUnknownAtomResult(node.Raw ?? string.Empty, node.AtomType ?? node.Type, $"Unsupported condition node type: {node.Type}")
                }
            }
        };
    }

    private ConditionEvaluationResult EvaluateAllOf(ConditionAstNode node, RuntimeGameState state)
    {
        if (node.Children.Count == 0)
        {
            return new ConditionEvaluationResult
            {
                Passed = true,
                HasUnknown = false,
                Reason = "AllOf has no children, so it passes by default."
            };
        }

        var childResults = node.Children
            .Select(child => this.EvaluateNode(child, state))
            .ToList();

        var atomResults = childResults
            .SelectMany(result => result.AtomResults)
            .ToList();

        var hasFalse = childResults.Any(result => result.Passed == false);
        var hasUnknown = childResults.Any(result => result.Passed is null || result.HasUnknown);
        var allTrue = childResults.All(result => result.Passed == true);

        return new ConditionEvaluationResult
        {
            Passed = allTrue ? true : hasFalse ? false : null,
            HasUnknown = hasUnknown,
            Reason = allTrue
                ? "AllOf passed: every child condition passed."
                : hasFalse
                    ? "AllOf failed: at least one child condition failed."
                    : "AllOf is unknown: no child failed, but at least one child could not be fully evaluated.",
            AtomResults = atomResults
        };
    }

    private ConditionEvaluationResult EvaluateAnyOf(ConditionAstNode node, RuntimeGameState state)
    {
        if (node.Children.Count == 0)
        {
            return new ConditionEvaluationResult
            {
                Passed = false,
                HasUnknown = false,
                Reason = "AnyOf has no children, so it fails by default."
            };
        }

        var childResults = node.Children
            .Select(child => this.EvaluateNode(child, state))
            .ToList();

        var atomResults = childResults
            .SelectMany(result => result.AtomResults)
            .ToList();

        var hasTrue = childResults.Any(result => result.Passed == true);
        var hasUnknown = childResults.Any(result => result.Passed is null || result.HasUnknown);
        var allFalse = childResults.All(result => result.Passed == false);

        return new ConditionEvaluationResult
        {
            Passed = hasTrue ? true : allFalse ? false : null,
            HasUnknown = !hasTrue && hasUnknown,
            Reason = hasTrue
                ? "AnyOf passed: at least one child condition passed."
                : allFalse
                    ? "AnyOf failed: every child condition failed."
                    : "AnyOf is unknown: no child passed, and at least one child could not be fully evaluated.",
            AtomResults = atomResults
        };
    }

    private ConditionEvaluationResult EvaluateNot(ConditionAstNode node, RuntimeGameState state)
    {
        var operandResult = this.EvaluateNode(node.Operand, state);
        bool? passed = operandResult.Passed switch
        {
            true => false,
            false => true,
            _ => (bool?)null
        };

        return new ConditionEvaluationResult
        {
            Passed = passed,
            HasUnknown = operandResult.Passed is null || operandResult.HasUnknown,
            Reason = passed switch
            {
                true => "Not passed: operand failed, so the negation passed.",
                false => "Not failed: operand passed, so the negation failed.",
                _ => "Not is unknown: operand could not be fully evaluated."
            },
            AtomResults = operandResult.AtomResults
        };
    }

    private ConditionEvaluationResult EvaluateAtom(ConditionAstNode node, RuntimeGameState state)
    {
        var atomType = node.AtomType ?? "Unknown";
        var raw = node.Raw ?? string.Empty;

        var atomResult = atomType switch
        {
            "Season" => this.EvaluateSeasonAtom(raw, node.Values, state),
            "DayOfMonth" or "Day" => this.EvaluateDayOfMonthAtom(raw, atomType, node.Values, state),
            "DayOfWeek" => this.EvaluateDayOfWeekAtom(raw, node.Values, state),
            "Time" => this.EvaluateTimeAtom(raw, node.Values, state),
            "Weather" => this.EvaluateWeatherAtom(raw, node.Values, state),
            "FestivalDay" => this.EvaluateFestivalDayAtom(raw, state),
            "Year" => this.EvaluateYearAtom(raw, node.Values, state),
            "Spouse" => this.EvaluateSpouseAtom(raw, node.Values, state),
            "IsHost" => this.CreateAtomResult(raw, "IsHost", true, "IsHost matched: offline/runtime export is treated as host."),
            "Friendship" => this.EvaluateFriendshipAtom(raw, node.Values, state),
            "Dating" => this.EvaluateDatingAtom(raw, node.Values, state),
            "Relationship" => this.EvaluateRelationshipAtom(raw, node.Values, state),
            "NpcVisibleHere" => this.EvaluateNpcVisibleHereAtom(raw, node.Values, state),
            "InUpgradedHouse" => this.EvaluateInUpgradedHouseAtom(raw, state),
            "SawEvent" => this.EvaluateSawEventAtom(raw, node.Values, state),
            "LocalMail" or "HostMail" or "HostOrLocalMail" => this.EvaluateMailAtom(raw, atomType, node.Values, state),
            "ChoseDialogueAnswers" => this.EvaluateDialogueAnswerAtom(raw, node.Values, state),
            "ActiveDialogueEvent" => this.EvaluateActiveDialogueEventAtom(raw, node.Values, state),
            "Unknown" => this.CreateUnknownAtomResult(raw, atomType, "Unknown atom cannot be evaluated."),
            _ => this.CreateUnknownAtomResult(raw, atomType, $"Unsupported atom type: {atomType}")
        };

        return new ConditionEvaluationResult
        {
            Passed = atomResult.Passed,
            HasUnknown = atomResult.Passed is null,
            Reason = atomResult.Reason,
            AtomResults = new List<ConditionAtomResult> { atomResult }
        };
    }

    private ConditionEvaluationResult EvaluateUnknown(ConditionAstNode node)
    {
        var atomResult = this.CreateUnknownAtomResult(
            node.Raw ?? string.Empty,
            node.AtomType ?? "Unknown",
            "Unknown condition node cannot be evaluated."
        );

        return new ConditionEvaluationResult
        {
            Passed = null,
            HasUnknown = true,
            Reason = atomResult.Reason,
            AtomResults = new List<ConditionAtomResult> { atomResult }
        };
    }

    private ConditionAtomResult EvaluateSeasonAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "Season", "Season condition has no candidate values.");
        }

        var matched = values.Any(value => string.Equals(state.Season, value, StringComparison.OrdinalIgnoreCase));
        return this.CreateAtomResult(
            raw,
            "Season",
            matched,
            matched
                ? $"Season matched: current {state.Season} is in [{string.Join(", ", values)}]"
                : $"Season failed: current {state.Season} is not in [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateDayOfMonthAtom(string raw, string atomType, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, atomType, $"{atomType} condition has no candidate values.");
        }

        var parsedDays = new List<int>();
        foreach (var value in values)
        {
            if (int.TryParse(value, out var parsedDay))
            {
                parsedDays.Add(parsedDay);
            }
        }

        if (parsedDays.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, atomType, $"{atomType} condition has no valid numeric values.");
        }

        var matched = parsedDays.Contains(state.DayOfMonth);
        return this.CreateAtomResult(
            raw,
            atomType,
            matched,
            matched
                ? $"{atomType} matched: current day {state.DayOfMonth} is in [{string.Join(", ", parsedDays)}]"
                : $"{atomType} failed: current day {state.DayOfMonth} is not in [{string.Join(", ", parsedDays)}]"
        );
    }

    private ConditionAtomResult EvaluateDayOfWeekAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "DayOfWeek", "DayOfWeek condition has no candidate values.");
        }

        var normalizedCurrent = NormalizeDayOfWeekName(state.DayOfWeek);
        var matched = values.Any(value =>
            string.Equals(normalizedCurrent, NormalizeDayOfWeekName(value), StringComparison.OrdinalIgnoreCase));
        return this.CreateAtomResult(
            raw,
            "DayOfWeek",
            matched,
            matched
                ? $"DayOfWeek matched: current {state.DayOfWeek} is in [{string.Join(", ", values)}]"
                : $"DayOfWeek failed: current {state.DayOfWeek} is not in [{string.Join(", ", values)}]"
        );
    }

    private static string NormalizeDayOfWeekName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return trimmed.ToUpperInvariant() switch
        {
            "MON" => "Monday",
            "TUE" => "Tuesday",
            "WED" => "Wednesday",
            "THU" => "Thursday",
            "FRI" => "Friday",
            "SAT" => "Saturday",
            "SUN" => "Sunday",
            "MONDAY" => "Monday",
            "TUESDAY" => "Tuesday",
            "WEDNESDAY" => "Wednesday",
            "THURSDAY" => "Thursday",
            "FRIDAY" => "Friday",
            "SATURDAY" => "Saturday",
            "SUNDAY" => "Sunday",
            _ => char.ToUpper(trimmed[0]) + trimmed[1..].ToLowerInvariant()
        };
    }

    private ConditionAtomResult EvaluateTimeAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count < 2)
        {
            return this.CreateUnknownAtomResult(raw, "Time", "Time condition requires [start, end] values.");
        }

        if (!int.TryParse(values[0], out var start) || !int.TryParse(values[1], out var end))
        {
            return this.CreateUnknownAtomResult(raw, "Time", "Time condition contains non-numeric bounds.");
        }

        var matched = state.Time >= start && state.Time <= end;
        return this.CreateAtomResult(
            raw,
            "Time",
            matched,
            matched
                ? $"Time matched: current {state.Time} is within [{start}, {end}]"
                : $"Time failed: current {state.Time} is outside [{start}, {end}]"
        );
    }

    private ConditionAtomResult EvaluateWeatherAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "Weather", "Weather condition has no candidate values.");
        }

        var matched = values.Any(value => string.Equals(state.Weather, value, StringComparison.OrdinalIgnoreCase));
        return this.CreateAtomResult(
            raw,
            "Weather",
            matched,
            matched
                ? $"Weather matched: current {state.Weather} is in [{string.Join(", ", values)}]"
                : $"Weather failed: current {state.Weather} is not in [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateFestivalDayAtom(string raw, RuntimeGameState state)
    {
        if (state.IsFestivalDay is null)
        {
            return this.CreateAtomResult(raw, "FestivalDay", false, "FestivalDay defaulted false: runtime festival state is unavailable.");
        }

        return this.CreateAtomResult(
            raw,
            "FestivalDay",
            state.IsFestivalDay.Value,
            state.IsFestivalDay.Value
                ? "FestivalDay matched: today is a festival day."
                : "FestivalDay failed: today is not a festival day."
        );
    }

    private ConditionAtomResult EvaluateYearAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "Year", "Year condition has no candidate values.");
        }

        var matched = values.Any(value => int.TryParse(value, out var parsedYear) && parsedYear == state.Year);
        return this.CreateAtomResult(
            raw,
            "Year",
            matched,
            matched
                ? $"Year matched: current year {state.Year} is in [{string.Join(", ", values)}]"
                : $"Year failed: current year {state.Year} is not in [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateSpouseAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "Spouse", "Spouse condition has no npc name.");
        }

        var npcName = values[0];
        var matched = IsListed(state.SpouseName, npcName)
            || IsListed(state.Spouse, npcName)
            || IsListed(state.MarriedTo, npcName)
            || IsListed(state.Spouses, npcName)
            || IsListed(state.EngagedTo, npcName);

        return this.CreateAtomResult(
            raw,
            "Spouse",
            matched,
            matched
                ? $"Spouse matched: player is married/engaged to {npcName}"
                : $"Spouse failed: player is not married/engaged to {npcName}"
        );
    }

    private ConditionAtomResult EvaluateFriendshipAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count < 2 || values.Count % 2 != 0)
        {
            return this.CreateUnknownAtomResult(raw, "Friendship", "Friendship condition requires npc/points pairs.");
        }

        var pairReasons = new List<string>();
        var allPassed = true;

        for (var index = 0; index < values.Count; index += 2)
        {
            var npcName = values[index];
            if (!int.TryParse(values[index + 1], out var requiredPoints))
            {
                return this.CreateUnknownAtomResult(raw, "Friendship", $"Friendship condition has invalid points value: {values[index + 1]}");
            }

            state.FriendshipPoints.TryGetValue(npcName, out var currentPoints);
            var passed = currentPoints >= requiredPoints;
            allPassed &= passed;
            pairReasons.Add(
                passed
                    ? $"{npcName} has {currentPoints}, requires {requiredPoints}"
                    : $"{npcName} has {currentPoints}, requires {requiredPoints}"
            );
        }

        return this.CreateAtomResult(
            raw,
            "Friendship",
            allPassed,
            allPassed
                ? $"Friendship matched: {string.Join("; ", pairReasons)}"
                : $"Friendship failed: {string.Join("; ", pairReasons)}"
        );
    }

    private ConditionAtomResult EvaluateDatingAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        {
            return this.CreateUnknownAtomResult(raw, "Dating", "Dating condition has no npc name.");
        }

        var npcName = values[0];
        var matched = state.DatingNpcNames.Contains(npcName);
        return this.CreateAtomResult(
            raw,
            "Dating",
            matched,
            matched
                ? $"Dating matched: player is dating {npcName}"
                : $"Dating failed: player is not dating {npcName}"
        );
    }

    private ConditionAtomResult EvaluateRelationshipAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count < 2 || string.IsNullOrWhiteSpace(values[0]))
        {
            return this.CreateUnknownAtomResult(raw, "Relationship", "Relationship condition requires npc and state list.");
        }

        var npcName = values[0].Trim();
        var allowedStates = values
            .Skip(1)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
        if (allowedStates.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "Relationship", "Relationship condition has no allowed states.");
        }

        var currentState = GetRelationshipState(npcName, state) ?? "None";
        var matched = allowedStates.Any(allowed =>
            string.Equals(allowed, currentState, StringComparison.OrdinalIgnoreCase));
        var allowedLabel = string.Join("/", allowedStates);

        return this.CreateAtomResult(
            raw,
            "Relationship",
            matched,
            matched
                ? $"{npcName}关系满足 {allowedLabel}"
                : $"{npcName}当前关系为 {currentState}，要求 {allowedLabel}");
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

    private ConditionAtomResult EvaluateNpcVisibleHereAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        {
            return this.CreateUnknownAtomResult(raw, "NpcVisibleHere", "NpcVisibleHere condition has no npc name.");
        }

        var npcName = values[0];
        var matched = state.VisibleNpcNamesHere.Contains(npcName);
        return this.CreateAtomResult(
            raw,
            "NpcVisibleHere",
            matched,
            matched
                ? $"NpcVisibleHere matched: {npcName} is currently visible in this location"
                : $"NpcVisibleHere failed: {npcName} is not currently visible in this location"
        );
    }

    private ConditionAtomResult EvaluateInUpgradedHouseAtom(string raw, RuntimeGameState state)
    {
        if (state.InUpgradedHouse is null)
        {
            return this.CreateUnknownAtomResult(raw, "InUpgradedHouse", "InUpgradedHouse runtime state is unavailable.");
        }

        return this.CreateAtomResult(
            raw,
            "InUpgradedHouse",
            state.InUpgradedHouse.Value,
            state.InUpgradedHouse.Value
                ? "InUpgradedHouse matched: player is currently inside an upgraded farmhouse or cabin."
                : "InUpgradedHouse failed: player is not currently inside an upgraded farmhouse or cabin."
        );
    }

    private ConditionAtomResult EvaluateSawEventAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "SawEvent", "SawEvent condition has no event ids.");
        }

        var matchedIds = values.Where(value => state.SeenEvents.Contains(value)).ToList();
        var matched = matchedIds.Count > 0;
        return this.CreateAtomResult(
            raw,
            "SawEvent",
            matched,
            matched
                ? $"SawEvent matched: player has seen [{string.Join(", ", matchedIds)}]"
                : $"SawEvent failed: player has not seen any of [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateMailAtom(string raw, string atomType, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, atomType, $"{atomType} condition has no mail ids.");
        }

        var matchedIds = values.Where(value => state.Mail.Contains(value)).ToList();
        var matched = matchedIds.Count > 0;
        return this.CreateAtomResult(
            raw,
            atomType,
            matched,
            matched
                ? $"Mail matched: player has {string.Join(", ", matchedIds)}"
                : $"Mail failed: player is missing all of [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateDialogueAnswerAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "ChoseDialogueAnswers", "ChoseDialogueAnswers condition has no answer ids.");
        }

        var matchedIds = values.Where(value => state.DialogueAnswers.Contains(value)).ToList();
        var matched = matchedIds.Count == values.Count;
        return this.CreateAtomResult(
            raw,
            "ChoseDialogueAnswers",
            matched,
            matched
                ? $"Dialogue answer matched: player has all of [{string.Join(", ", values)}]"
                : $"Dialogue answer failed: player has [{string.Join(", ", matchedIds)}], requires [{string.Join(", ", values)}]"
        );
    }

    private ConditionAtomResult EvaluateActiveDialogueEventAtom(string raw, IReadOnlyList<string> values, RuntimeGameState state)
    {
        if (values.Count == 0)
        {
            return this.CreateUnknownAtomResult(raw, "ActiveDialogueEvent", "ActiveDialogueEvent condition has no topic id.");
        }

        var topic = values[0];
        if (state.ActiveDialogueEventsKnown)
        {
            var isActive = state.ActiveDialogueEvents.Contains(topic);
            return this.CreateAtomResult(
                raw,
                "ActiveDialogueEvent",
                isActive,
                isActive
                    ? $"ActiveDialogueEvent matched: topic '{topic}' is currently active."
                    : $"ActiveDialogueEvent failed: topic '{topic}' is not currently active.");
        }

        var inRecordedTopics = state.DialogueAnswers.Contains(topic);
        return this.CreateAtomResult(
            raw,
            "ActiveDialogueEvent",
            inRecordedTopics,
            inRecordedTopics
                ? $"ActiveDialogueEvent conservative match: topic '{topic}' appears in exported dialogue/topic records."
                : $"ActiveDialogueEvent conservative match: topic '{topic}' not found in exported dialogue/topic records.");
    }

    private ConditionAtomResult CreateAtomResult(string raw, string atomType, bool passed, string reason)
    {
        return new ConditionAtomResult
        {
            Raw = raw,
            AtomType = atomType,
            Passed = passed,
            IsContextSensitive = ContextSensitiveAtomTypes.Contains(atomType),
            IsProgressionSensitive = ProgressionSensitiveAtomTypes.Contains(atomType),
            Reason = reason
        };
    }

    private ConditionAtomResult CreateUnknownAtomResult(string raw, string atomType, string reason)
    {
        return new ConditionAtomResult
        {
            Raw = raw,
            AtomType = atomType,
            Passed = null,
            IsContextSensitive = ContextSensitiveAtomTypes.Contains(atomType),
            IsProgressionSensitive = ProgressionSensitiveAtomTypes.Contains(atomType),
            Reason = reason
        };
    }
}
