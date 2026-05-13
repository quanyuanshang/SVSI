using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class EventScriptChoiceIndexParser
{
    private const string DataEventsPrefix = "Data/Events/";
    private const int PreviewLength = 160;

    private static readonly Regex QuestionClauseRegex = new(
        @"\$q\s+([^\s#]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    private static readonly Regex ResponseIdRegex = new(
        @"\$r\s+([^\s#/]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    private readonly EventKeySplitter eventKeySplitter = new();

    public EventScriptChoiceIndex Build(IEnumerable<ScannedMod> scannedMods)
    {
        var entries = new List<EventScriptChoiceEntry>();

        foreach (var mod in scannedMods)
        {
            foreach (var entry in this.BuildEntriesForMod(mod))
            {
                entries.Add(entry);
            }
        }

        return new EventScriptChoiceIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            EntryCount = entries.Count,
            Entries = entries
        };
    }

    private IEnumerable<EventScriptChoiceEntry> BuildEntriesForMod(ScannedMod mod)
    {
        if (!mod.IsContentPatcherContentPack || mod.ContentJson is not JsonObject contentJsonObject)
        {
            yield break;
        }

        if (contentJsonObject["Changes"] is not JsonArray changes)
        {
            yield break;
        }

        for (var changeIndex = 0; changeIndex < changes.Count; changeIndex++)
        {
            if (changes[changeIndex] is not JsonObject change)
            {
                continue;
            }

            var assetTarget = this.ReadString(change, "Target");
            if (!assetTarget.StartsWith(DataEventsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var action = this.ReadString(change, "Action");
            if (string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in this.BuildEditDataEntries(mod, change, changeIndex, assetTarget))
                {
                    yield return entry;
                }

                continue;
            }

            if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in this.BuildLoadEntries(mod, change, changeIndex, assetTarget))
                {
                    yield return entry;
                }
            }
        }
    }

    private IEnumerable<EventScriptChoiceEntry> BuildEditDataEntries(
        ScannedMod mod,
        JsonObject change,
        int changeIndex,
        string assetTarget)
    {
        if (change["Entries"] is not JsonObject entries)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (entry.Value is null)
            {
                continue;
            }

            var rawKey = entry.Key;
            var rawScript = this.ReadRawValue(entry.Value);
            var choiceEntry = this.CreateChoiceEntry(
                mod,
                assetTarget,
                rawKey,
                rawScript,
                new List<EvidenceRef>
                {
                    new EvidenceRef
                    {
                        Kind = "content-json-entry",
                        SourcePath = mod.ContentJsonPath ?? string.Empty,
                        JsonPath = $"$.Changes[{changeIndex}].Entries[\"{EscapeForJsonPath(rawKey)}\"]"
                    }
                }
            );

            if (choiceEntry is not null)
            {
                yield return choiceEntry;
            }
        }
    }

    private IEnumerable<EventScriptChoiceEntry> BuildLoadEntries(
        ScannedMod mod,
        JsonObject change,
        int changeIndex,
        string assetTarget)
    {
        var fromFile = this.ReadString(change, "FromFile");
        if (string.IsNullOrWhiteSpace(fromFile) || fromFile.Contains("{{", StringComparison.Ordinal))
        {
            yield break;
        }

        var resolvedPath = Path.GetFullPath(
            Path.Combine(mod.DirectoryPath, fromFile.Replace('/', Path.DirectorySeparatorChar))
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

        foreach (var property in loadedEntries.Properties())
        {
            var rawKey = property.Name;
            var rawScript = property.Value.Type == JTokenType.String
                ? property.Value.Value<string>() ?? string.Empty
                : property.Value.ToString(Newtonsoft.Json.Formatting.None);
            var choiceEntry = this.CreateChoiceEntry(
                mod,
                assetTarget,
                rawKey,
                rawScript,
                new List<EvidenceRef>
                {
                    new EvidenceRef
                    {
                        Kind = "content-json-change",
                        SourcePath = mod.ContentJsonPath ?? string.Empty,
                        JsonPath = $"$.Changes[{changeIndex}]"
                    },
                    new EvidenceRef
                    {
                        Kind = "load-file-entry",
                        SourcePath = resolvedPath,
                        JsonPath = $"$[\"{EscapeForJsonPath(rawKey)}\"]"
                    }
                }
            );

            if (choiceEntry is not null)
            {
                yield return choiceEntry;
            }
        }
    }

    private EventScriptChoiceEntry? CreateChoiceEntry(
        ScannedMod mod,
        string assetTarget,
        string rawKey,
        string rawScript,
        List<EvidenceRef> evidenceRefs)
    {
        var questionIds = ExtractQuestionIds(rawScript);
        var responseIds = ExtractMatches(ResponseIdRegex, rawScript);
        if (questionIds.Count == 0 && responseIds.Count == 0)
        {
            return null;
        }

        var keySplit = this.eventKeySplitter.Split(rawKey);
        return new EventScriptChoiceEntry
        {
            SourceModId = mod.UniqueID,
            SourceModName = mod.Name,
            EventId = keySplit.EventId,
            AssetTarget = assetTarget,
            Location = ExtractLocation(assetTarget),
            RawKey = rawKey,
            RawScript = rawScript,
            PreviewText = BuildPreview(rawScript),
            QuestionIds = questionIds,
            ResponseIds = responseIds,
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

    private static List<string> ExtractQuestionIds(string rawScript)
    {
        return QuestionClauseRegex
            .Matches(rawScript)
            .Select(match => match.Groups[1].Value)
            .SelectMany(value => value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ExtractMatches(Regex regex, string rawScript)
    {
        return regex
            .Matches(rawScript)
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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

    private static string EscapeForJsonPath(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
