using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class TranslationCatalogBuilder
{
    private static readonly Regex I18nTokenPattern = new(@"^\{\{i18n:(?<key>[^}]+)\}\}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ContainsChinesePattern = new(@"[\u3400-\u9FFF]", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> VanillaLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FarmHouse"] = "农舍",
        ["Town"] = "鹈鹕镇",
        ["Farm"] = "农场",
        ["Forest"] = "煤矿森林",
        ["Beach"] = "海滩",
        ["Mountain"] = "山区",
        ["Railroad"] = "铁路",
        ["Mine"] = "矿井",
        ["Mines"] = "矿井",
        ["Desert"] = "沙漠",
        ["IslandSouth"] = "姜岛南部",
        ["IslandNorth"] = "姜岛北部",
        ["IslandWest"] = "姜岛西部",
        ["IslandEast"] = "姜岛东部",
        ["Saloon"] = "星之果实小酒馆",
        ["SeedShop"] = "皮埃尔杂货店",
        ["AnimalShop"] = "玛妮牧场",
        ["Hospital"] = "诊所",
        ["ScienceHouse"] = "罗宾家",
        ["ElliottHouse"] = "艾利欧特小屋",
        ["Trailer"] = "潘姆拖车",
        ["ManorHouse"] = "市长宅邸",
        ["WizardHouse"] = "法师塔",
        ["CommunityCenter"] = "社区中心",
        ["Sewer"] = "下水道",
        ["BusStop"] = "巴士站",
        ["FishShop"] = "威利渔具店",
        ["ArchaeologyHouse"] = "博物馆",
        ["Backwoods"] = "农场后山",
        ["Custom_GrampletonSuburbsTrainStation"] = "格兰普顿郊区火车站",
    };

    private static readonly IReadOnlyDictionary<string, string> VanillaCharacters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Sam"] = "山姆",
        ["Abigail"] = "阿比盖尔",
        ["Sebastian"] = "塞巴斯蒂安",
        ["Penny"] = "潘妮",
        ["Haley"] = "海莉",
        ["Leah"] = "莉亚",
        ["Maru"] = "玛鲁",
        ["Alex"] = "亚历克斯",
        ["Elliott"] = "艾利欧特",
        ["Shane"] = "谢恩",
        ["Harvey"] = "哈维",
        ["Emily"] = "艾米丽",
        ["Wizard"] = "法师",
        ["Lewis"] = "刘易斯",
        ["Marnie"] = "玛妮",
        ["Robin"] = "罗宾",
        ["Demetrius"] = "迪米特里",
        ["Linus"] = "莱纳斯",
        ["Pierre"] = "皮埃尔",
        ["Caroline"] = "卡洛琳",
        ["Jodi"] = "乔迪",
        ["Kent"] = "肯特",
        ["Vincent"] = "文森特",
        ["Jas"] = "贾斯",
        ["Pam"] = "潘姆",
        ["Gus"] = "格斯",
        ["Clint"] = "克林特",
        ["Willy"] = "威利",
        ["Evelyn"] = "艾芙琳",
        ["George"] = "乔治",
        ["Dwarf"] = "矮人",
        ["Krobus"] = "科罗布斯",
        ["Sandy"] = "桑迪",
        ["Leo"] = "雷欧",
        ["Pelette"] = "佩莱特",
    };

    private sealed class CatalogAccumulator
    {
        private readonly Dictionary<string, TranslationEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TranslationWarning> warnings = new();

        public void Add(
            string category,
            string raw,
            string zh,
            string source,
            string? sourceModId = null,
            string? sourceModName = null,
            string? sourcePath = null)
        {
            if (string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(raw) ||
                string.IsNullOrWhiteSpace(zh))
            {
                return;
            }

            var key = BuildKey(category, raw, sourceModId);
            if (this.entries.ContainsKey(key))
            {
                return;
            }

            this.entries[key] = new TranslationEntry
            {
                Category = category,
                Raw = raw.Trim(),
                Zh = zh.Trim(),
                Source = source,
                SourceModId = sourceModId,
                SourceModName = sourceModName,
                SourcePath = sourcePath
            };
        }

        public void Warn(string message, ScannedMod? mod = null, string? sourcePath = null)
        {
            this.warnings.Add(new TranslationWarning
            {
                Message = message,
                SourceModId = mod?.UniqueID,
                SourceModName = mod?.Name,
                SourcePath = sourcePath
            });
        }

        public TranslationCatalog Build()
        {
            return new TranslationCatalog
            {
                Entries = this.entries.Values
                    .OrderBy(entry => entry.Category, StringComparer.Ordinal)
                    .ThenBy(entry => entry.SourceModId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Raw, StringComparer.Ordinal)
                    .ToList(),
                Warnings = this.warnings
            };
        }

        private static string BuildKey(string category, string raw, string? sourceModId)
        {
            return $"{sourceModId ?? string.Empty}|{category}|{raw.Trim()}";
        }
    }

    public TranslationCatalog Build(IEnumerable<ScannedMod> scannedMods)
    {
        var catalog = new CatalogAccumulator();

        AddSharedEntries(catalog, VanillaLocations, "location", "vanilla-export");
        AddSharedEntries(catalog, VanillaCharacters, "npc", "vanilla-export");

        foreach (var mod in scannedMods)
        {
            this.AddModTranslations(catalog, mod);
        }

        return catalog.Build();
    }

    private void AddModTranslations(CatalogAccumulator catalog, ScannedMod mod)
    {
        if (string.IsNullOrWhiteSpace(mod.DirectoryPath) || !Directory.Exists(mod.DirectoryPath))
        {
            return;
        }

        var i18n = this.LoadI18nMap(catalog, mod);
        this.AddHeuristicI18nEntries(catalog, mod, i18n);
        this.AddContentJsonEntries(catalog, mod, i18n);
        this.AddStructuredLocationEntries(catalog, mod, i18n);
        this.AddLooseDataEntries(catalog, mod, i18n);
    }

    private Dictionary<string, string> LoadI18nMap(CatalogAccumulator catalog, ScannedMod mod)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i18nDirectory = Path.Combine(mod.DirectoryPath, "i18n");
        if (!Directory.Exists(i18nDirectory))
        {
            return results;
        }

        foreach (var fileName in new[] { "zh-CN.json", "zh.json", "default.json" })
        {
            var path = Path.Combine(i18nDirectory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var root = LooseJsonParser.ParseNodeFromText(File.ReadAllText(path)) as JsonObject;
                if (root is null)
                {
                    continue;
                }

                foreach (var pair in root)
                {
                    if (pair.Value is JsonValue value &&
                        value.TryGetValue<string>(out var text) &&
                        !string.IsNullOrWhiteSpace(text) &&
                        !results.ContainsKey(pair.Key))
                    {
                        results[pair.Key] = text.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                catalog.Warn($"Failed to parse translation file: {ex.Message}", mod, path);
            }
        }

        return results;
    }

    private void AddHeuristicI18nEntries(
        CatalogAccumulator catalog,
        ScannedMod mod,
        IReadOnlyDictionary<string, string> i18n)
    {
        foreach (var pair in i18n)
        {
            if (!ContainsChinese(pair.Value))
            {
                continue;
            }

            if (TryMapI18nKey(pair.Key, out var category, out var raw))
            {
                catalog.Add(category, raw, pair.Value, "mod-i18n", mod.UniqueID, mod.Name, $"i18n:{pair.Key}");
            }

            foreach (var (derivedCategory, derivedRaw) in GetAdditionalI18nMappings(pair.Key))
            {
                catalog.Add(derivedCategory, derivedRaw, pair.Value, "mod-i18n", mod.UniqueID, mod.Name, $"i18n:{pair.Key}");
            }
        }
    }

    private void AddContentJsonEntries(
        CatalogAccumulator catalog,
        ScannedMod mod,
        IReadOnlyDictionary<string, string> i18n)
    {
        if (mod.ContentJson is not JsonObject root)
        {
            return;
        }

        if (root["Changes"] is not JsonArray changes)
        {
            return;
        }

        foreach (var changeNode in changes.OfType<JsonObject>())
        {
            var target = changeNode["Target"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (changeNode["Entries"] is JsonObject entries)
            {
                if (TryResolveCategoryFromTarget(target, out var category, out var targetRaw))
                {
                    if (!string.IsNullOrWhiteSpace(targetRaw))
                    {
                        var display = ResolveChangeDisplayName(changeNode, i18n);
                        if (!string.IsNullOrWhiteSpace(display))
                        {
                            catalog.Add(category, targetRaw, display, "content", mod.UniqueID, mod.Name, mod.ContentJsonPath);
                        }
                    }

                    // For Data/Events/<location> targets, the Entries values are EVENT
                    // SCRIPTS keyed by event-key (e.g. "11451861/k 11451864"). Those
                    // are not display names and must not enter the translation
                    // catalog as location translations (would otherwise show
                    // event-script gibberish under `category=location`). The
                    // location-level translation, if any, comes from the patch's
                    // own DisplayName/Name and is handled above.
                    var skipEntryIteration = IsDataEventsTarget(target);
                    if (!skipEntryIteration)
                    {
                        foreach (var entry in entries)
                        {
                            var raw = entry.Key;
                            var display = ResolveDisplayNameFromNode(entry.Value, i18n);
                            if (!string.IsNullOrWhiteSpace(display))
                            {
                                catalog.Add(category, raw, display, "content", mod.UniqueID, mod.Name, mod.ContentJsonPath);
                            }
                        }
                    }
                }
            }

            if (TryResolveCategoryFromTarget(target, out var fileCategory, out _) &&
                ResolveDisplayNameFromNode(changeNode, i18n) is { Length: > 0 } directDisplay &&
                changeNode["Id"]?.GetValue<string>() is { Length: > 0 } directRaw)
            {
                catalog.Add(fileCategory, directRaw, directDisplay, "content", mod.UniqueID, mod.Name, mod.ContentJsonPath);
            }
        }
    }

    private void AddLooseDataEntries(
        CatalogAccumulator catalog,
        ScannedMod mod,
        IReadOnlyDictionary<string, string> i18n)
    {
        var candidates = new[]
        {
            ("Data\\Characters", "npc"),
            ("Data\\NPCDispositions", "npc"),
            ("Data\\Locations", "location"),
            ("Data\\Objects", "item"),
            ("Characters\\Dialogue", "npc"),
        };

        foreach (var (relativeDirectory, category) in candidates)
        {
            var directoryPath = Path.Combine(mod.DirectoryPath, relativeDirectory);
            if (!Directory.Exists(directoryPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var root = LooseJsonParser.ParseNodeFromText(File.ReadAllText(filePath)) as JsonObject;
                    if (root is null)
                    {
                        continue;
                    }

                    foreach (var pair in root)
                    {
                        var display = ResolveDisplayNameFromNode(pair.Value, i18n);
                        if (!string.IsNullOrWhiteSpace(display))
                        {
                            catalog.Add(category, pair.Key, display, "content", mod.UniqueID, mod.Name, filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    catalog.Warn($"Failed to parse content data file: {ex.Message}", mod, filePath);
                }
            }
        }
    }

    private void AddStructuredLocationEntries(
        CatalogAccumulator catalog,
        ScannedMod mod,
        IReadOnlyDictionary<string, string> i18n)
    {
        var candidateFiles = new[]
        {
            Path.Combine(mod.DirectoryPath, "assets", "DependencyData", "CJBCheats.json"),
            Path.Combine(mod.DirectoryPath, "code", "Locations", "CJBWarps.json"),
            Path.Combine(mod.DirectoryPath, "code", "Locations", "WorldMap.json"),
            Path.Combine(mod.DirectoryPath, "code", "Locations", "LocationsData.json"),
        };

        foreach (var filePath in candidateFiles)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            try
            {
                var root = LooseJsonParser.ParseNodeFromText(File.ReadAllText(filePath));
                this.CollectStructuredLocationEntries(catalog, mod, i18n, root, filePath, null);
            }
            catch (Exception ex)
            {
                catalog.Warn($"Failed to parse structured location file: {ex.Message}", mod, filePath);
            }
        }
    }

    private void CollectStructuredLocationEntries(
        CatalogAccumulator catalog,
        ScannedMod mod,
        IReadOnlyDictionary<string, string> i18n,
        JsonNode? node,
        string sourcePath,
        string? currentKey)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var location = ReadStructuredLocationRaw(obj, currentKey);
                var display = ReadStructuredLocationDisplay(obj, i18n);
                if (!string.IsNullOrWhiteSpace(location) &&
                    !string.IsNullOrWhiteSpace(display) &&
                    ContainsChinese(display))
                {
                    catalog.Add("location", location, display, "structured-content", mod.UniqueID, mod.Name, sourcePath);
                }

                foreach (var child in obj)
                {
                    this.CollectStructuredLocationEntries(catalog, mod, i18n, child.Value, sourcePath, child.Key);
                }

                break;
            }

            case JsonArray array:
                foreach (var child in array)
                {
                    this.CollectStructuredLocationEntries(catalog, mod, i18n, child, sourcePath, currentKey);
                }

                break;
        }
    }

    private static void AddSharedEntries(
        CatalogAccumulator catalog,
        IReadOnlyDictionary<string, string> entries,
        string category,
        string source)
    {
        foreach (var pair in entries)
        {
            catalog.Add(category, pair.Key, pair.Value, source);
        }
    }

    private static bool IsDataEventsTarget(string target)
    {
        var normalized = target.Replace('\\', '/');
        return normalized.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveCategoryFromTarget(
        string target,
        out string category,
        out string? targetRaw)
    {
        category = string.Empty;
        targetRaw = null;
        var normalized = target.Replace('\\', '/');

        if (normalized.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase))
        {
            category = "location";
            targetRaw = normalized[(normalized.LastIndexOf('/') + 1)..];
            return true;
        }

        if (normalized.Equals("Data/Locations", StringComparison.OrdinalIgnoreCase))
        {
            category = "location";
            return true;
        }

        if (normalized.Equals("Data/Characters", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Data/NPCDispositions", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Characters/Dialogue", StringComparison.OrdinalIgnoreCase))
        {
            category = "npc";
            return true;
        }

        if (normalized.Equals("Data/Objects", StringComparison.OrdinalIgnoreCase))
        {
            category = "item";
            return true;
        }

        return false;
    }

    private static string? ResolveChangeDisplayName(JsonObject changeNode, IReadOnlyDictionary<string, string> i18n)
    {
        foreach (var key in new[] { "DisplayName", "Name", "ChineseName", "LocalizedName", "Title" })
        {
            if (changeNode[key] is JsonNode node)
            {
                var display = ResolveString(node, i18n);
                if (!string.IsNullOrWhiteSpace(display))
                {
                    return display;
                }
            }
        }

        return null;
    }

    private static string? ResolveDisplayNameFromNode(JsonNode? node, IReadOnlyDictionary<string, string> i18n)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue)
        {
            var text = ResolveString(node, i18n);
            return ContainsChinese(text) ? text : null;
        }

        if (node is JsonObject obj)
        {
            foreach (var key in new[] { "DisplayName", "Name", "ChineseName", "LocalizedName", "Title" })
            {
                if (obj[key] is JsonNode fieldNode)
                {
                    var display = ResolveString(fieldNode, i18n);
                    if (!string.IsNullOrWhiteSpace(display))
                    {
                        return display;
                    }
                }
            }
        }

        return null;
    }

    private static string? ReadStructuredLocationRaw(JsonObject obj, string? currentKey)
    {
        foreach (var key in new[] { "Location", "MapName", "LocationName", "Id" })
        {
            if (obj[key] is JsonValue value &&
                value.TryGetValue<string>(out var text))
            {
                var trimmed = text.Trim();
                if (trimmed.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(currentKey) &&
            currentKey.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
        {
            return currentKey.Trim();
        }

        return null;
    }

    private static string? ReadStructuredLocationDisplay(JsonObject obj, IReadOnlyDictionary<string, string> i18n)
    {
        foreach (var key in new[] { "DisplayName", "ScrollText", "LocationName", "Name", "Text" })
        {
            if (obj[key] is JsonNode node)
            {
                var resolved = ResolveString(node, i18n);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
        }

        return null;
    }

    private static string? ResolveString(JsonNode node, IReadOnlyDictionary<string, string> i18n)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var tokenMatch = I18nTokenPattern.Match(trimmed);
        if (tokenMatch.Success &&
            i18n.TryGetValue(tokenMatch.Groups["key"].Value.Trim(), out var translated))
        {
            return translated.Trim();
        }

        return trimmed;
    }

    private static bool TryMapI18nKey(string key, out string category, out string raw)
    {
        category = string.Empty;
        raw = string.Empty;

        var normalized = key.Replace('-', '.');
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        if (TryMapSpecialI18nKey(parts, out category, out raw))
        {
            return true;
        }

        var head = parts[0].ToLowerInvariant();
        if (head is "npc" or "npcs" or "character" or "characters")
        {
            category = "npc";
            raw = parts[1];
            return true;
        }

        if (head is "location" or "locations")
        {
            category = "location";
            raw = parts[1];
            return true;
        }

        if (head is "item" or "items")
        {
            category = "item";
            raw = parts[1];
            return true;
        }

        return false;
    }

    private static bool TryMapSpecialI18nKey(string[] parts, out string category, out string raw)
    {
        category = string.Empty;
        raw = string.Empty;

        if (parts.Length >= 2 &&
            string.Equals(parts[0], "loc", StringComparison.OrdinalIgnoreCase))
        {
            category = "location";
            raw = parts[1];
            return true;
        }

        if (parts.Length >= 3 &&
            string.Equals(parts[^1], "DisplayName", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[0], "SDS", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 3)
            {
                category = "npc";
                raw = parts[1];
                return true;
            }

            var token = parts[^2];
            if (token.StartsWith("SDS_", StringComparison.OrdinalIgnoreCase))
            {
                category = "location";
                raw = BuildCustomSdsLocationName(token);
                return true;
            }
        }

        if (parts.Length == 2 &&
            string.Equals(parts[0], "DisplayName", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(parts[1], "MarnieShed", StringComparison.OrdinalIgnoreCase))
            {
                category = "location";
                raw = "Custom_MarnieShed";
                return true;
            }

            if (string.Equals(parts[1], "GrampletonSuburbs", StringComparison.OrdinalIgnoreCase))
            {
                category = "location";
                raw = "Custom_GrampletonSuburbs";
                return true;
            }

            if (string.Equals(parts[1], "GrampletonSuburbsTrainStation", StringComparison.OrdinalIgnoreCase))
            {
                category = "location";
                raw = "Custom_GrampletonSuburbsTrainStation";
                return true;
            }
        }

        if (parts.Length == 2 &&
            string.Equals(parts[0], "LocationData", StringComparison.OrdinalIgnoreCase))
        {
            switch (parts[1])
            {
                case "Grampleton_Fields":
                    category = "location";
                    raw = "Custom_GrampletonFields";
                    return true;
                case "Grampleton_Suburbs":
                    category = "location";
                    raw = "Custom_GrampletonSuburbs";
                    return true;
                case "Grampleton_Train":
                    category = "location";
                    raw = "Custom_GrampletonSuburbsTrainStation";
                    return true;
                case "Grampleton_Outskirts":
                    category = "location";
                    raw = "Custom_GrampletonSuburbsOutskirts";
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Category, string Raw)> GetAdditionalI18nMappings(string key)
    {
        if (string.Equals(key, "LocationData.Grampleton_Fields", StringComparison.OrdinalIgnoreCase))
        {
            yield return ("location", "Custom_GrampletonFields_Small");
            yield return ("location", "Custom_GrampletonFields_small");
        }
    }

    private static string BuildCustomSdsLocationName(string token)
    {
        var suffix = token.Substring("SDS_".Length);
        if (suffix.StartsWith("Woods", StringComparison.OrdinalIgnoreCase))
        {
            return $"Custom_{suffix}";
        }

        return $"Custom_SDS.{suffix}";
    }

    private static bool ContainsChinese(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && ContainsChinesePattern.IsMatch(value);
    }
}
