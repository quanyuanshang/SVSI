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
        "Location"
    };

    private static readonly HashSet<string> ProgressionSensitiveAtomTypes = new(StringComparer.Ordinal)
    {
        "Friendship",
        "SawEvent",
        "Mail",
        "LocalMail",
        "HostMail",
        "HostOrLocalMail",
        "ChoseDialogueAnswers"
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
            "Friendship" => this.EvaluateFriendshipAtom(raw, node.Values, state),
            "SawEvent" => this.EvaluateSawEventAtom(raw, node.Values, state),
            "LocalMail" or "HostMail" or "HostOrLocalMail" => this.EvaluateMailAtom(raw, atomType, node.Values, state),
            "ChoseDialogueAnswers" => this.EvaluateDialogueAnswerAtom(raw, node.Values, state),
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

        var matched = values.Any(value => string.Equals(state.DayOfWeek, value, StringComparison.OrdinalIgnoreCase));
        return this.CreateAtomResult(
            raw,
            "DayOfWeek",
            matched,
            matched
                ? $"DayOfWeek matched: current {state.DayOfWeek} is in [{string.Join(", ", values)}]"
                : $"DayOfWeek failed: current {state.DayOfWeek} is not in [{string.Join(", ", values)}]"
        );
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
