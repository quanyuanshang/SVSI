using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class EventIndexBuilder
{
    private const string DataEventsPrefix = "Data/Events/";
    private const int PreviewLength = 160;
    private static readonly Regex BranchReferencePattern = new(
        @"(?:^|[/\\\s])(?:fork|switchEvent)\s+([A-Za-z0-9_.:-]+)(?:\s+([A-Za-z0-9_.:-]+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private readonly EventKeySplitter eventKeySplitter = new();
    private readonly EventPreconditionParser eventPreconditionParser = new();

    private sealed class ChangeContext
    {
        public JsonObject Change { get; init; } = new();

        public string SourcePath { get; init; } = string.Empty;

        public string BaseDirectory { get; init; } = string.Empty;

        public string JsonPath { get; init; } = string.Empty;
    }

    public StoryRawEventIndex Build(IEnumerable<ScannedMod> scannedMods)
    {
        var nodes = new List<StoryNode>();

        foreach (var mod in scannedMods)
        {
            foreach (var node in this.BuildNodesForMod(mod))
            {
                nodes.Add(node);
            }
        }

        var distinctNodes = nodes
            .GroupBy(node => $"{node.AssetTarget}|{node.RawKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var modConfigs = scannedMods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.UniqueID))
            .GroupBy(mod => mod.UniqueID.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Dictionary<string, string>(group.Last().ConfigValues, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        return new StoryRawEventIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            NodeCount = distinctNodes.Count,
            Nodes = distinctNodes,
            ModConfigByUniqueId = modConfigs
        };
    }

    private IEnumerable<StoryNode> BuildNodesForMod(ScannedMod mod)
    {
        if (!mod.IsContentPatcherContentPack || mod.ContentJson is not JsonObject contentJsonObject)
        {
            yield break;
        }

        var rootSourcePath = mod.ContentJsonPath ?? Path.Combine(mod.DirectoryPath, "content.json");
        var visitedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dynamicTokens = this.ReadDynamicTokenDefinitionsFromContentTree(contentJsonObject, rootSourcePath);

        foreach (var changeContext in this.EnumerateChanges(contentJsonObject, rootSourcePath, visitedSourcePaths))
        {
            var assetTarget = this.ReadString(changeContext.Change, "Target");
            if (!assetTarget.StartsWith(DataEventsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var action = this.ReadString(changeContext.Change, "Action");
            if (string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var node in this.BuildEditDataNodes(mod, changeContext, assetTarget, dynamicTokens))
                {
                    yield return node;
                }

                continue;
            }

            if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var node in this.BuildLoadNodes(mod, changeContext, assetTarget, dynamicTokens))
                {
                    yield return node;
                }
            }
        }
    }

    private IEnumerable<StoryNode> BuildEditDataNodes(
        ScannedMod mod,
        ChangeContext changeContext,
        string assetTarget,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens)
    {
        var change = changeContext.Change;
        if (change["Entries"] is not JsonObject entries)
        {
            yield break;
        }

        var branchTargets = ReadBranchTargets(entries);

        foreach (var entry in entries)
        {
            if (entry.Value is null)
            {
                continue;
            }

            var rawKey = entry.Key;
            var rawScript = this.ReadRawValue(entry.Value);
            if (IsBranchOnlyEntry(rawKey, branchTargets))
            {
                continue;
            }

            yield return this.CreateStoryNode(
                mod,
                assetTarget,
                rawKey,
                rawScript,
                dynamicTokens,
                ReadPatchWhenConditions(change),
                branchTargets,
                new List<EvidenceRef>
                {
                    new EvidenceRef
                    {
                        Kind = "content-json-entry",
                        SourcePath = changeContext.SourcePath,
                        JsonPath = $"{changeContext.JsonPath}.Entries[\"{EscapeForJsonPath(rawKey)}\"]"
                    }
                }
            );
        }
    }

    private IEnumerable<StoryNode> BuildLoadNodes(
        ScannedMod mod,
        ChangeContext changeContext,
        string assetTarget,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens)
    {
        var change = changeContext.Change;
        var fromFile = this.ReadString(change, "FromFile");
        if (string.IsNullOrWhiteSpace(fromFile) || fromFile.Contains("{{", StringComparison.Ordinal))
        {
            yield break;
        }

        var resolvedPath = Path.GetFullPath(
            Path.Combine(changeContext.BaseDirectory, fromFile.Replace('/', Path.DirectorySeparatorChar))
        );

        if (!File.Exists(resolvedPath))
        {
            yield break;
        }

        JObject? loadedEntries;
        try
        {
            loadedEntries = LooseJsonParser.ParseTokenFromFile(resolvedPath) as JObject;
        }
        catch
        {
            yield break;
        }

        if (loadedEntries is null)
        {
            yield break;
        }

        var branchTargets = ReadBranchTargets(loadedEntries);

        foreach (var property in loadedEntries.Properties())
        {
            var rawKey = property.Name;
            var rawScript = property.Value.Type == JTokenType.String
                ? property.Value.Value<string>() ?? string.Empty
                : property.Value.ToString(Newtonsoft.Json.Formatting.None);
            if (IsBranchOnlyEntry(rawKey, branchTargets))
            {
                continue;
            }

            yield return this.CreateStoryNode(
                mod,
                assetTarget,
                rawKey,
                rawScript,
                dynamicTokens,
                ReadPatchWhenConditions(change),
                branchTargets,
                new List<EvidenceRef>
                {
                    new EvidenceRef
                    {
                        Kind = "content-json-change",
                        SourcePath = changeContext.SourcePath,
                        JsonPath = changeContext.JsonPath
                    },
                    new EvidenceRef
                    {
                        Kind = "load-file-entry",
                        SourcePath = resolvedPath,
                        JsonPath = $"$[\"{EscapeForJsonPath(rawKey)}\"]"
                    }
                }
            );
        }
    }

    private IEnumerable<ChangeContext> EnumerateChanges(
        JsonObject root,
        string sourcePath,
        ISet<string> visitedSourcePaths)
    {
        if (!visitedSourcePaths.Add(sourcePath))
        {
            yield break;
        }

        if (root["Changes"] is not JsonArray changes)
        {
            yield break;
        }

        for (var changeIndex = 0; changeIndex < changes.Count; changeIndex++)
        {
            if (changes[changeIndex] is not JsonObject change)
            {
                continue;
            }

            var jsonPath = $"$.Changes[{changeIndex}]";
            var action = this.ReadString(change, "Action");
            if (string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var includedFilePath in this.ReadIncludedFilePaths(change, sourcePath))
                {
                    JsonObject? includedRoot;
                    try
                    {
                        includedRoot = LooseJsonParser.ParseNodeFromFile(includedFilePath) as JsonObject;
                    }
                    catch
                    {
                        continue;
                    }

                    if (includedRoot is null)
                    {
                        continue;
                    }

                    foreach (var includedChangeContext in this.EnumerateChanges(
                        includedRoot,
                        includedFilePath,
                        visitedSourcePaths))
                    {
                        yield return includedChangeContext;
                    }
                }

                continue;
            }

            yield return new ChangeContext
            {
                Change = change,
                SourcePath = sourcePath,
                BaseDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty,
                JsonPath = jsonPath
            };
        }
    }

    private IEnumerable<string> ReadIncludedFilePaths(JsonObject change, string sourcePath)
    {
        if (change["FromFile"] is JsonArray pathArray)
        {
            foreach (var item in pathArray)
            {
                if (item is null)
                {
                    continue;
                }

                foreach (var resolvedPath in this.SplitAndResolveIncludedPaths(
                    item.GetValue<string>(),
                    sourcePath))
                {
                    yield return resolvedPath;
                }
            }

            yield break;
        }

        var fromFile = this.ReadString(change, "FromFile");
        foreach (var resolvedPath in this.SplitAndResolveIncludedPaths(fromFile, sourcePath))
        {
            yield return resolvedPath;
        }
    }

    private IEnumerable<string> SplitAndResolveIncludedPaths(string rawPathList, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(rawPathList))
        {
            yield break;
        }

        var baseDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        foreach (var rawPath in rawPathList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(rawPath) || rawPath.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetFullPath(
                Path.Combine(baseDirectory, rawPath.Replace('/', Path.DirectorySeparatorChar))
            );
        }
    }

    private StoryNode CreateStoryNode(
        ScannedMod mod,
        string assetTarget,
        string rawKey,
        string rawScript,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens,
        List<PatchWhenCondition> patchWhenConditions,
        HashSet<string> branchTargets,
        List<EvidenceRef> evidenceRefs)
    {
        var location = ExtractLocation(assetTarget);
        var keySplit = this.eventKeySplitter.Split(rawKey);
        var parsedConditions = this.ParsePreconditions(keySplit.PreconditionFragments, dynamicTokens, mod);
        var fingerprint = $"{mod.UniqueID}|{assetTarget}|{rawKey}|{string.Join("|", evidenceRefs.Select(refItem => refItem.SourcePath + "::" + refItem.JsonPath))}";

        return new StoryNode
        {
            NodeId = $"story-node:{ComputeShortHash(fingerprint)}",
            EventId = keySplit.EventId,
            EventKind = DetermineEventKind(
                keySplit.EventId,
                keySplit.PreconditionFragments,
                rawScript,
                branchTargets.Contains(keySplit.EventId)),
            SourceModId = mod.UniqueID,
            SourceModName = mod.Name,
            AssetTarget = assetTarget,
            Location = location,
            RawKey = rawKey,
            RawPreconditions = keySplit.PreconditionFragments,
            PatchWhenConditions = patchWhenConditions,
            ConditionAst = parsedConditions.ConditionAst,
            UnknownFragments = parsedConditions.UnknownFragments,
            RawScriptPreview = BuildPreview(rawScript),
            EvidenceRefs = evidenceRefs,
            SourceModConfigValues = mod.ConfigValues,
            SourceModDynamicTokens = dynamicTokens.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private EventPreconditionParseResult ParsePreconditions(
        IReadOnlyList<string> rawPreconditionFragments,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens,
        ScannedMod mod)
    {
        var children = new List<ConditionAstNode>();
        var unknownFragments = new List<string>();

        foreach (var rawFragment in rawPreconditionFragments)
        {
            if (TryExpandDynamicTokenFragment(rawFragment, dynamicTokens, out var expandedNode))
            {
                children.Add(expandedNode);
                continue;
            }

            var expandedFragment = ExpandBracedPlaceholders(rawFragment, mod, dynamicTokens, out _);
            var parsed = this.eventPreconditionParser.Parse(new[] { expandedFragment });
            children.AddRange(parsed.ConditionAst.Children);
            unknownFragments.AddRange(parsed.UnknownFragments);
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

    private static string ExpandBracedPlaceholders(
        string fragment,
        ScannedMod mod,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens,
        out List<string> unresolvedTokens)
    {
        var unresolved = new List<string>();
        var result = Regex.Replace(
            fragment,
            @"\{\{([A-Za-z0-9_]+)\}\}",
            match =>
            {
                var name = match.Groups[1].Value;
                if (TryResolvePlaceholderValue(name, mod, dynamicTokens, out var replacement))
                {
                    return replacement;
                }

                unresolved.Add(name);
                return match.Value;
            });
        unresolvedTokens = unresolved;
        return result;
    }

    private static bool TryResolvePlaceholderValue(
        string tokenName,
        ScannedMod mod,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens,
        out string replacement)
    {
        replacement = string.Empty;
        if (mod.ConfigValues.TryGetValue(tokenName, out var configValue) && !string.IsNullOrWhiteSpace(configValue))
        {
            replacement = configValue.Trim();
            return true;
        }

        if (!dynamicTokens.TryGetValue(tokenName, out var definitions))
        {
            return false;
        }

        foreach (var definition in definitions)
        {
            var value = definition.Value?.Trim() ?? string.Empty;
            if (value.Length > 0 && definition.WhenConditions.Count == 0)
            {
                replacement = value;
                return true;
            }

            if (Regex.IsMatch(value, @"^\d+$"))
            {
                replacement = value;
                return true;
            }
        }

        return false;
    }

    private bool TryExpandDynamicTokenFragment(
        string rawFragment,
        Dictionary<string, List<DynamicTokenDefinition>> dynamicTokens,
        out ConditionAstNode expandedNode)
    {
        expandedNode = new ConditionAstNode
        {
            Type = "Unknown",
            Raw = rawFragment
        };

        var match = Regex.Match(rawFragment.Trim(), @"^\{\{([A-Za-z0-9_]+)\}\}$");
        if (!match.Success)
        {
            return false;
        }

        var tokenName = match.Groups[1].Value;
        if (!dynamicTokens.TryGetValue(tokenName, out var definitions) || definitions.Count == 0)
        {
            return false;
        }

        var alternatives = new List<ConditionAstNode>();
        foreach (var definition in definitions)
        {
            var alternativeChildren = new List<ConditionAstNode>();

            foreach (var whenFragment in definition.WhenFragments)
            {
                var parsedWhen = this.eventPreconditionParser.Parse(new[] { whenFragment });
                alternativeChildren.AddRange(parsedWhen.ConditionAst.Children);
            }

            var valueFragments = this.eventKeySplitter.Split($"0/{definition.Value}").PreconditionFragments;
            foreach (var valueFragment in valueFragments)
            {
                var parsedValue = this.eventPreconditionParser.Parse(new[] { valueFragment });
                alternativeChildren.AddRange(parsedValue.ConditionAst.Children);
            }

            alternatives.Add(alternativeChildren.Count switch
            {
                0 => new ConditionAstNode
                {
                    Type = "Unknown",
                    Raw = rawFragment
                },
                1 => alternativeChildren[0],
                _ => new ConditionAstNode
                {
                    Type = "AllOf",
                    Children = alternativeChildren
                }
            });
        }

        expandedNode = alternatives.Count == 1
            ? alternatives[0]
            : new ConditionAstNode
            {
                Type = "AnyOf",
                Children = alternatives
            };
        return true;
    }

    private string ReadString(JsonObject source, string propertyName)
    {
        return source[propertyName]?.GetValue<string>() ?? string.Empty;
    }

    private string ReadRawValue(JsonNode value)
    {
        return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
            ? stringValue
            : value.ToJsonString();
    }

    private static List<PatchWhenCondition> ReadPatchWhenConditions(JsonObject change)
    {
        var conditions = new List<PatchWhenCondition>();
        if (change["When"] is not JsonObject whenObject)
        {
            return conditions;
        }

        foreach (var condition in whenObject)
        {
            conditions.Add(new PatchWhenCondition
            {
                Key = condition.Key,
                Value = ReadConditionValue(condition.Value),
                RawValue = condition.Value?.ToJsonString() ?? string.Empty,
                IsKnown = false,
                Reason = "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator."
            });
        }

        return conditions;
    }

    private static string ReadConditionValue(JsonNode? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (value is JsonValue booleanValue && booleanValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        return value.ToJsonString();
    }

    private Dictionary<string, List<DynamicTokenDefinition>> ReadDynamicTokenDefinitionsFromContentTree(
        JsonObject root,
        string sourcePath)
    {
        var definitionsByName = ReadDynamicTokenDefinitions(root, sourcePath);
        var visitedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sourcePath };
        foreach (var includedPath in this.EnumerateIncludedContentFiles(root, sourcePath, visitedSourcePaths))
        {
            JsonObject? includedRoot;
            try
            {
                includedRoot = LooseJsonParser.ParseNodeFromFile(includedPath) as JsonObject;
            }
            catch
            {
                continue;
            }

            if (includedRoot is null)
            {
                continue;
            }

            MergeDynamicTokenDefinitions(definitionsByName, ReadDynamicTokenDefinitions(includedRoot, includedPath));
        }

        return definitionsByName;
    }

    private IEnumerable<string> EnumerateIncludedContentFiles(
        JsonObject root,
        string sourcePath,
        ISet<string> visitedSourcePaths)
    {
        if (root["Changes"] is not JsonArray changes)
        {
            yield break;
        }

        foreach (var changeNode in changes)
        {
            if (changeNode is not JsonObject change
                || !string.Equals(this.ReadString(change, "Action"), "Include", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var includedPath in this.ReadIncludedFilePaths(change, sourcePath))
            {
                if (!visitedSourcePaths.Add(includedPath))
                {
                    continue;
                }

                yield return includedPath;

                JsonObject? includedRoot;
                try
                {
                    includedRoot = LooseJsonParser.ParseNodeFromFile(includedPath) as JsonObject;
                }
                catch
                {
                    continue;
                }

                if (includedRoot is null)
                {
                    continue;
                }

                foreach (var nestedIncludedPath in this.EnumerateIncludedContentFiles(
                    includedRoot,
                    includedPath,
                    visitedSourcePaths))
                {
                    yield return nestedIncludedPath;
                }
            }
        }
    }

    private static void MergeDynamicTokenDefinitions(
        Dictionary<string, List<DynamicTokenDefinition>> destination,
        Dictionary<string, List<DynamicTokenDefinition>> source)
    {
        foreach (var pair in source)
        {
            if (!destination.TryGetValue(pair.Key, out var definitions))
            {
                definitions = new List<DynamicTokenDefinition>();
                destination[pair.Key] = definitions;
            }

            definitions.AddRange(pair.Value);
        }
    }

    private static Dictionary<string, List<DynamicTokenDefinition>> ReadDynamicTokenDefinitions(
        JsonObject root,
        string sourcePath)
    {
        var definitionsByName = new Dictionary<string, List<DynamicTokenDefinition>>(StringComparer.Ordinal);
        if (root["DynamicTokens"] is not JsonArray dynamicTokensArray)
        {
            return definitionsByName;
        }

        foreach (var tokenNode in dynamicTokensArray)
        {
            if (tokenNode is not JsonObject tokenObject)
            {
                continue;
            }

            var name = ReadConditionValue(tokenObject["Name"])?.Trim();
            var value = ReadConditionValue(tokenObject["Value"])?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!definitionsByName.TryGetValue(name, out var definitions))
            {
                definitions = new List<DynamicTokenDefinition>();
                definitionsByName[name] = definitions;
            }

            ReadDynamicTokenWhen(
                tokenObject["When"] as JsonObject,
                out var whenConditions,
                out var whenFragments);
            definitions.Add(new DynamicTokenDefinition
            {
                Name = name,
                Value = value,
                WhenConditions = whenConditions,
                WhenFragments = whenFragments,
                SourceFile = sourcePath
            });
        }

        return definitionsByName;
    }

    private static void ReadDynamicTokenWhen(
        JsonObject? whenObject,
        out List<PatchWhenCondition> whenConditions,
        out List<string> whenFragments)
    {
        whenConditions = new List<PatchWhenCondition>();
        whenFragments = new List<string>();
        if (whenObject is null)
        {
            return;
        }

        foreach (var condition in whenObject)
        {
            var value = ReadConditionValue(condition.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            whenConditions.Add(new PatchWhenCondition
            {
                Key = condition.Key,
                Value = value,
                RawValue = condition.Value?.ToJsonString() ?? string.Empty,
                IsKnown = false,
                Reason = "DynamicToken guard condition pending evaluation."
            });

            if (string.Equals(condition.Key, "Season", StringComparison.OrdinalIgnoreCase))
            {
                whenFragments.Add($"Season {value}");
            }
        }
    }

    private static HashSet<string> ReadBranchTargets(JsonObject entries)
    {
        var branchTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Value is null)
            {
                continue;
            }

            var script = entry.Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
                ? stringValue
                : entry.Value.ToJsonString();

            CollectBranchTargets(script, branchTargets);
        }

        return branchTargets;
    }

    private static HashSet<string> ReadBranchTargets(JObject entries)
    {
        var branchTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in entries.Properties())
        {
            var script = property.Value.Type == JTokenType.String
                ? property.Value.Value<string>() ?? string.Empty
                : property.Value.ToString(Newtonsoft.Json.Formatting.None);

            CollectBranchTargets(script, branchTargets);
        }

        return branchTargets;
    }

    private static void CollectBranchTargets(string script, HashSet<string> branchTargets)
    {
        foreach (Match match in BranchReferencePattern.Matches(script))
        {
            for (var groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
            {
                var captured = match.Groups[groupIndex].Value;
                if (!string.IsNullOrWhiteSpace(captured))
                {
                    branchTargets.Add(captured);
                }
            }
        }
    }

    private static bool IsBranchOnlyEntry(string rawKey, ISet<string> branchTargets)
    {
        return !rawKey.Contains('/', StringComparison.Ordinal) &&
            branchTargets.Contains(rawKey);
    }

    private static string ExtractLocation(string assetTarget)
    {
        return assetTarget.StartsWith(DataEventsPrefix, StringComparison.OrdinalIgnoreCase)
            ? assetTarget[DataEventsPrefix.Length..]
            : assetTarget;
    }

    private static string BuildPreview(string rawScript)
    {
        var compact = rawScript
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();

        while (compact.Contains("  ", StringComparison.Ordinal))
        {
            compact = compact.Replace("  ", " ", StringComparison.Ordinal);
        }

        return compact.Length <= PreviewLength
            ? compact
            : compact[..(PreviewLength - 3)] + "...";
    }

    private static string ComputeShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static string EscapeForJsonPath(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static StoryNodeEventKind DetermineEventKind(
        string eventId,
        IReadOnlyList<string> rawPreconditions,
        string rawScript,
        bool isIndexedBranchTarget)
    {
        if (isIndexedBranchTarget)
        {
            return StoryNodeEventKind.BranchTarget;
        }

        if (IsNumericEventId(eventId))
        {
            return StoryNodeEventKind.RegularLocationEvent;
        }

        if (IsSpecialGameEventId(eventId))
        {
            return StoryNodeEventKind.SpecialGameEvent;
        }

        if (IsBranchLikeEventId(eventId))
        {
            return StoryNodeEventKind.BranchTarget;
        }

        if (rawPreconditions.Count > 0)
        {
            return StoryNodeEventKind.RegularLocationEvent;
        }

        if (string.IsNullOrWhiteSpace(rawScript) || rawScript.Trim().Length < 8)
        {
            return StoryNodeEventKind.DialogueOnly;
        }

        return StoryNodeEventKind.BranchTarget;
    }

    private static bool IsNumericEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
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

    private static bool IsBranchLikeEventId(string eventId)
    {
        return string.Equals(eventId, "end", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventId, "continue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventId, "healer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventId, "stop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventId, "abort", StringComparison.OrdinalIgnoreCase)
            || eventId.StartsWith("date", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialGameEventId(string eventId)
    {
        return string.Equals(eventId, "PlayerKilled", StringComparison.OrdinalIgnoreCase)
            || eventId.StartsWith("MaggHealer", StringComparison.OrdinalIgnoreCase)
            || eventId.StartsWith("MaggMage", StringComparison.OrdinalIgnoreCase);
    }
}
