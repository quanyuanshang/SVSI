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

        return new StoryRawEventIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            NodeCount = distinctNodes.Count,
            Nodes = distinctNodes
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
                foreach (var node in this.BuildEditDataNodes(mod, changeContext, assetTarget))
                {
                    yield return node;
                }

                continue;
            }

            if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var node in this.BuildLoadNodes(mod, changeContext, assetTarget))
                {
                    yield return node;
                }
            }
        }
    }

    private IEnumerable<StoryNode> BuildEditDataNodes(
        ScannedMod mod,
        ChangeContext changeContext,
        string assetTarget)
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
                ReadPatchWhenConditions(change),
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
        string assetTarget)
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
                ReadPatchWhenConditions(change),
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
        List<PatchWhenCondition> patchWhenConditions,
        List<EvidenceRef> evidenceRefs)
    {
        var location = ExtractLocation(assetTarget);
        var keySplit = this.eventKeySplitter.Split(rawKey);
        var parsedConditions = this.eventPreconditionParser.Parse(keySplit.PreconditionFragments);
        var fingerprint = $"{mod.UniqueID}|{assetTarget}|{rawKey}|{string.Join("|", evidenceRefs.Select(refItem => refItem.SourcePath + "::" + refItem.JsonPath))}";

        return new StoryNode
        {
            NodeId = $"story-node:{ComputeShortHash(fingerprint)}",
            EventId = keySplit.EventId,
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
            EvidenceRefs = evidenceRefs
        };
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
}
