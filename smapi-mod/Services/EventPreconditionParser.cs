using System.Text;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class EventPreconditionParser
{
    private static readonly Dictionary<string, string> PositiveAliasMap = new(StringComparer.Ordinal)
    {
        ["*"] = "WorldState",
        ["*n"] = "HostOrLocalMail",
        ["a"] = "Tile",
        ["b"] = "ReachedMineBottom",
        ["B"] = "SpouseBed",
        ["s"] = "Season",
        ["c"] = "FreeInventorySlots",
        ["D"] = "Dating",
        ["t"] = "Time",
        ["w"] = "Weather",
        ["f"] = "Friendship",
        ["e"] = "SawEvent",
        ["G"] = "GameStateQuery",
        ["h"] = "MissingPet",
        ["Hn"] = "HostMail",
        ["i"] = "HasItem",
        ["q"] = "ChoseDialogueAnswers",
        ["J"] = "JojaBundlesDone",
        ["L"] = "InUpgradedHouse",
        ["m"] = "EarnedMoney",
        ["M"] = "HasMoney",
        ["N"] = "GoldenWalnuts",
        ["n"] = "LocalMail",
        ["y"] = "Year",
        ["O"] = "Spouse",
        ["u"] = "DayOfMonth",
        ["j"] = "DaysPlayed",
        ["g"] = "Gender",
        ["p"] = "NpcVisibleHere",
        ["v"] = "NPCVisible",
        ["r"] = "Random",
        ["C"] = "CommunityCenterOrWarehouseDone",
        ["H"] = "IsHost",
        ["R"] = "Roommate",
        ["S"] = "SawSecretNote"
    };

    private static readonly Dictionary<string, string> NegativeAliasMap = new(StringComparer.Ordinal)
    {
        ["o"] = "Spouse",
        ["k"] = "SawEvent",
        ["d"] = "DayOfWeek",
        ["l"] = "LocalMail",
        ["Hl"] = "HostMail",
        ["*l"] = "HostOrLocalMail",
        ["Rf"] = "Roommate",
        ["z"] = "Season",
        ["U"] = "UpcomingFestival",
        ["A"] = "ActiveDialogueEvent",
        ["F"] = "FestivalDay",
        ["X"] = "CommunityCenterOrWarehouseDone"
    };

    public EventPreconditionParseResult Parse(IEnumerable<string> rawPreconditionFragments)
    {
        var children = new List<ConditionAstNode>();
        var unknownFragments = new List<string>();

        foreach (var rawFragment in rawPreconditionFragments)
        {
            if (string.IsNullOrWhiteSpace(rawFragment))
            {
                continue;
            }

            var parsed = this.ParseFragment(rawFragment.Trim(), unknownFragments);
            if (parsed is not null)
            {
                children.Add(parsed);
            }
        }

        return new EventPreconditionParseResult
        {
            ConditionAst = new ConditionAstNode
            {
                Type = "AllOf",
                Children = children
            },
            UnknownFragments = unknownFragments
        };
    }

    private ConditionAstNode? ParseFragment(string rawFragment, List<string> unknownFragments)
    {
        if (string.IsNullOrWhiteSpace(rawFragment))
        {
            return null;
        }

        if (rawFragment.StartsWith("!", StringComparison.Ordinal))
        {
            var innerRaw = rawFragment[1..].TrimStart();
            if (string.IsNullOrWhiteSpace(innerRaw))
            {
                unknownFragments.Add(rawFragment);
                return new ConditionAstNode
                {
                    Type = "Not",
                    Operand = new ConditionAstNode
                    {
                        Type = "Unknown",
                        Raw = innerRaw
                    }
                };
            }

            return new ConditionAstNode
            {
                Type = "Not",
                Operand = this.ParsePositiveFragment(innerRaw, rawFragment, unknownFragments)
            };
        }

        return this.ParsePositiveFragment(rawFragment, rawFragment, unknownFragments);
    }

    private ConditionAstNode ParsePositiveFragment(
        string normalizedRawFragment,
        string originalRawFragment,
        List<string> unknownFragments)
    {
        var tokens = Tokenize(normalizedRawFragment);
        if (tokens.Count == 0)
        {
            unknownFragments.Add(originalRawFragment);
            return new ConditionAstNode
            {
                Type = "Unknown",
                Raw = normalizedRawFragment
            };
        }

        var keyword = tokens[0];
        var values = tokens.Skip(1).ToList();

        if (NegativeAliasMap.TryGetValue(keyword, out var negativeCanonicalName))
        {
            return new ConditionAstNode
            {
                Type = "Not",
                Operand = this.ParseRecognizedKeyword(
                    negativeCanonicalName,
                    keyword,
                    normalizedRawFragment,
                    originalRawFragment,
                    values,
                    unknownFragments
                )
            };
        }

        var canonicalName = NormalizeKeyword(keyword);
        if (canonicalName is null)
        {
            unknownFragments.Add(originalRawFragment);
            return new ConditionAstNode
            {
                Type = "Unknown",
                Raw = normalizedRawFragment
            };
        }

        return this.ParseRecognizedKeyword(
            canonicalName,
            keyword,
            normalizedRawFragment,
            originalRawFragment,
            values,
            unknownFragments
        );
    }

    private ConditionAstNode ParseRecognizedKeyword(
        string canonicalName,
        string keyword,
        string normalizedRawFragment,
        string originalRawFragment,
        List<string> values,
        List<string> unknownFragments)
    {
        return canonicalName switch
        {
            "Friendship" => this.ParseFriendship(keyword, normalizedRawFragment, originalRawFragment, values, unknownFragments),
            "SawEvent" => this.ParseSawEvent(keyword, normalizedRawFragment, values),
            "ChoseDialogueAnswers" => this.ParseChoseDialogueAnswers(keyword, normalizedRawFragment, values),
            _ => CreateAtom(canonicalName, normalizedRawFragment, values)
        };
    }

    private ConditionAstNode ParseFriendship(
        string keyword,
        string normalizedRawFragment,
        string originalRawFragment,
        List<string> values,
        List<string> unknownFragments)
    {
        if (values.Count == 0)
        {
            return CreateAtom("Friendship", normalizedRawFragment, values);
        }

        if (values.Count % 2 != 0)
        {
            unknownFragments.Add(originalRawFragment);
            return new ConditionAstNode
            {
                Type = "Unknown",
                Raw = normalizedRawFragment
            };
        }

        var atoms = new List<ConditionAstNode>();
        for (var index = 0; index < values.Count; index += 2)
        {
            var pairValues = new List<string>
            {
                values[index],
                values[index + 1]
            };
            atoms.Add(CreateAtom("Friendship", $"{keyword} {pairValues[0]} {pairValues[1]}", pairValues));
        }

        return atoms.Count == 1
            ? atoms[0]
            : new ConditionAstNode
            {
                Type = "AllOf",
                Children = atoms
            };
    }

    private ConditionAstNode ParseSawEvent(string keyword, string normalizedRawFragment, List<string> values)
    {
        if (values.Count <= 1)
        {
            return CreateAtom("SawEvent", normalizedRawFragment, values);
        }

        return new ConditionAstNode
        {
            Type = "AnyOf",
            Children = values
                .Select(value => CreateAtom("SawEvent", $"{keyword} {value}", new List<string> { value }))
                .ToList()
        };
    }

    private ConditionAstNode ParseChoseDialogueAnswers(string keyword, string normalizedRawFragment, List<string> values)
    {
        if (values.Count <= 1)
        {
            return CreateAtom("ChoseDialogueAnswers", normalizedRawFragment, values);
        }

        return new ConditionAstNode
        {
            Type = "AllOf",
            Children = values
                .Select(value => CreateAtom("ChoseDialogueAnswers", $"{keyword} {value}", new List<string> { value }))
                .ToList()
        };
    }

    private static string? NormalizeKeyword(string keyword)
    {
        if (PositiveAliasMap.TryGetValue(keyword, out var canonicalName))
        {
            return canonicalName;
        }

        return keyword.ToLowerInvariant() switch
        {
            "activedialogueevent" => "ActiveDialogueEvent",
            "communitycenterorwarehousedone" => "CommunityCenterOrWarehouseDone",
            "season" => "Season",
            "dayofmonth" => "DayOfMonth",
            "dayofweek" => "DayOfWeek",
            "festivalday" => "FestivalDay",
            "freeinventoryslots" => "FreeInventorySlots",
            "dating" => "Dating",
            "time" => "Time",
            "weather" => "Weather",
            "friendship" => "Friendship",
            "sawevent" => "SawEvent",
            "gamestatequery" => "GameStateQuery",
            "localmail" => "LocalMail",
            "hostmail" => "HostMail",
            "hostorlocalmail" => "HostOrLocalMail",
            "chosedialogueanswers" => "ChoseDialogueAnswers",
            "inupgradedhouse" => "InUpgradedHouse",
            "jojabundlesdone" => "JojaBundlesDone",
            "goldenwalnuts" => "GoldenWalnuts",
            "earnedmoney" => "EarnedMoney",
            "hasmoney" => "HasMoney",
            "missingpet" => "MissingPet",
            "hasitem" => "HasItem",
            "spouse" => "Spouse",
            "spousebed" => "SpouseBed",
            "year" => "Year",
            "daysplayed" => "DaysPlayed",
            "gender" => "Gender",
            "npcvisiblehere" => "NpcVisibleHere",
            "npcvisible" => "NPCVisible",
            "ishost" => "IsHost",
            "roommate" => "Roommate",
            "random" => "Random",
            "reachedminebottom" => "ReachedMineBottom",
            "sawsecretnote" => "SawSecretNote",
            "tile" => "Tile",
            "upcomingfestival" => "UpcomingFestival",
            "worldstate" => "WorldState",
            _ => null
        };
    }

    private static ConditionAstNode CreateAtom(string atomType, string raw, List<string> values)
    {
        return new ConditionAstNode
        {
            Type = "Atom",
            AtomType = atomType,
            Raw = raw,
            Values = values
        };
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder(text.Length);
        var inQuotes = false;
        var isEscaped = false;

        foreach (var ch in text)
        {
            if (isEscaped)
            {
                current.Append(ch);
                isEscaped = false;
                continue;
            }

            if (ch == '\\' && inQuotes)
            {
                isEscaped = true;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
