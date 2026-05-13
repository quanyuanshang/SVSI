using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class DialogueIndexParser
{
    private const string DialoguePrefix = "Characters/Dialogue/";
    private const int PreviewLength = 160;

    private static readonly Regex ResponseIdRegex = new(
        @"\$r\s+([^\s#/]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    private static readonly Regex LinkedEventIdRegex = new(
        @"\$v\s+([^\s#/]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    public DialogueIndex Build(IEnumerable<ScannedMod> scannedMods)
    {
        var entries = new List<DialogueIndexEntry>();

        foreach (var mod in scannedMods)
        {
            foreach (var entry in this.BuildEntriesForMod(mod))
            {
                entries.Add(entry);
            }
        }

        return new DialogueIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            EntryCount = entries.Count,
            Entries = entries
        };
    }

    private IEnumerable<DialogueIndexEntry> BuildEntriesForMod(ScannedMod mod)
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
            if (!assetTarget.StartsWith(DialoguePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var action = this.ReadString(change, "Action");
            if (!string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (change["Entries"] is not JsonObject dialogueEntries)
            {
                continue;
            }

            var npcName = ExtractNpcName(assetTarget);
            foreach (var entry in dialogueEntries)
            {
                if (entry.Value is null)
                {
                    continue;
                }

                var dialogueKey = entry.Key;
                var rawDialogue = this.ReadRawValue(entry.Value);

                yield return new DialogueIndexEntry
                {
                    SourceModId = mod.UniqueID,
                    SourceModName = mod.Name,
                    NpcName = npcName,
                    DialogueKey = dialogueKey,
                    RawDialogue = rawDialogue,
                    PreviewText = BuildPreview(rawDialogue),
                    ResponseIds = ExtractMatches(ResponseIdRegex, rawDialogue),
                    LinkedEventIds = ExtractMatches(LinkedEventIdRegex, rawDialogue),
                    EvidenceRefs =
                    {
                        new EvidenceRef
                        {
                            Kind = "content-json-entry",
                            SourcePath = mod.ContentJsonPath ?? string.Empty,
                            JsonPath = $"$.Changes[{changeIndex}].Entries[\"{EscapeForJsonPath(dialogueKey)}\"]"
                        }
                    }
                };
            }
        }
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

    private static string ExtractNpcName(string assetTarget)
    {
        return assetTarget.StartsWith(DialoguePrefix, StringComparison.OrdinalIgnoreCase)
            ? assetTarget[DialoguePrefix.Length..]
            : assetTarget;
    }

    private static string BuildPreview(string rawDialogue)
    {
        var compact = rawDialogue
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

    private static List<string> ExtractMatches(Regex regex, string rawDialogue)
    {
        return regex
            .Matches(rawDialogue)
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string EscapeForJsonPath(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
