using System.Text.Json.Nodes;
using StardewStoryInspector.Models;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class TranslationCatalogBuilderTests
{
    public static void RunAll()
    {
        Build_IncludesVanillaFallbackEntries();
        Build_PrefersModI18nAndContentEntries();
        Build_MapsLocAndSdsDisplayNameKeys();
        Build_ReadsStructuredLocationDisplayFiles();
        Build_ReadsWorldMapAndLocationsDataLocationDisplays();
        Build_CollectsWarningsForInvalidJson();
        Build_DataEventsEntriesDoNotPolluteLocationCatalog();
    }

    private static void Build_IncludesVanillaFallbackEntries()
    {
        var catalog = new TranslationCatalogBuilder().Build(Array.Empty<ScannedMod>());

        AssertContainsEntry(catalog, "location", "FarmHouse", "农舍", "vanilla-export");
        AssertContainsEntry(catalog, "npc", "Sebastian", "塞巴斯蒂安", "vanilla-export");
    }

    private static void Build_PrefersModI18nAndContentEntries()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(modDirectory, "i18n"));
            File.WriteAllText(
                Path.Combine(modDirectory, "i18n", "zh-CN.json"),
                "{\n" +
                "  \"characters.Pelette.name\": \"佩莱特\"\n" +
                "}"
            );

            var contentJson = JsonNode.Parse(
                "{\n" +
                "  \"Changes\": [\n" +
                "    {\n" +
                "      \"Action\": \"EditData\",\n" +
                "      \"Target\": \"Data/Events/Custom_GrampletonSuburbsTrainStation\",\n" +
                "      \"DisplayName\": \"格兰普尔顿郊区火车站\",\n" +
                "      \"Entries\": {\n" +
                "        \"demo\": \"event body\"\n" +
                "      }\n" +
                "    }\n" +
                "  ]\n" +
                "}"
            );

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "Example Mod",
                UniqueID = "example.mod",
                ContentJsonPath = Path.Combine(modDirectory, "content.json"),
                ContentJson = contentJson
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            AssertContainsEntry(catalog, "npc", "Pelette", "佩莱特", "mod-i18n", "example.mod");
            AssertContainsEntry(
                catalog,
                "location",
                "Custom_GrampletonSuburbsTrainStation",
                "格兰普尔顿郊区火车站",
                "content",
                "example.mod");
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void Build_DataEventsEntriesDoNotPolluteLocationCatalog()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            var contentJson = JsonNode.Parse(
                "{\n" +
                "  \"Changes\": [\n" +
                "    {\n" +
                "      \"Action\": \"EditData\",\n" +
                "      \"Target\": \"Data/Events/Farm\",\n" +
                "      \"Entries\": {\n" +
                "        \"11451861/k 11451864\": \"echos/-2000 -2000/farmer 0 0 0 Shane 0 0 0/warp farmer 64 15/speak Shane \\\"中文剧本台词\\\"/end\",\n" +
                "        \"11451862/w rainy/e 11451861\": \"distantBanjo/-100 -100/中文事件脚本/end\"\n" +
                "      }\n" +
                "    },\n" +
                "    {\n" +
                "      \"Action\": \"EditData\",\n" +
                "      \"Target\": \"Data/Objects\",\n" +
                "      \"Entries\": {\n" +
                "        \"mymod.SpecialItem\": { \"Name\": \"特殊物品\" }\n" +
                "      }\n" +
                "    }\n" +
                "  ]\n" +
                "}"
            );

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "Event Pack",
                UniqueID = "event.pack",
                ContentJsonPath = Path.Combine(modDirectory, "content.json"),
                ContentJson = contentJson
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            foreach (var entry in catalog.Entries)
            {
                if (string.Equals(entry.Category, "location", StringComparison.OrdinalIgnoreCase) &&
                    entry.Raw.Contains('/', StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Data/Events entry leaked into translation catalog as a location: raw='{entry.Raw}', zh='{entry.Zh}'.");
                }

                if (string.Equals(entry.Category, "location", StringComparison.OrdinalIgnoreCase) &&
                    entry.Zh.Contains("/end", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Event-script text leaked into translation catalog as a location zh value: raw='{entry.Raw}'.");
                }
            }

            AssertContainsEntry(catalog, "item", "mymod.SpecialItem", "特殊物品", "content", "event.pack");
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void Build_CollectsWarningsForInvalidJson()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(modDirectory, "i18n"));
            File.WriteAllText(Path.Combine(modDirectory, "i18n", "zh.json"), "{ invalid");

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "Broken Mod",
                UniqueID = "broken.mod"
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            if (catalog.Warnings.Count == 0)
            {
                throw new InvalidOperationException("Invalid i18n JSON should produce a warning.");
            }
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void Build_MapsLocAndSdsDisplayNameKeys()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(modDirectory, "i18n"));
            File.WriteAllText(
                Path.Combine(modDirectory, "i18n", "zh-CN.json"),
                "{\n" +
                "  \"loc.Custom_MarnieShed\": \"玛妮的棚屋\",\n" +
                "  \"SDS.Uriel.DisplayName\": \"乌列\",\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_Woods2.DisplayName\": \"隐秘森林\",\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_Caveroom.DisplayName\": \"废弃矿坑隐藏房间\",\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_cliff.DisplayName\": \"废弃观景台\"\n" +
                "}"
            );

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "Heuristic Mod",
                UniqueID = "heuristic.mod"
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            AssertContainsEntry(catalog, "location", "Custom_MarnieShed", "玛妮的棚屋", "mod-i18n", "heuristic.mod");
            AssertContainsEntry(catalog, "npc", "Uriel", "乌列", "mod-i18n", "heuristic.mod");
            AssertContainsEntry(catalog, "location", "Custom_Woods2", "隐秘森林", "mod-i18n", "heuristic.mod");
            AssertContainsEntry(catalog, "location", "Custom_SDS.Caveroom", "废弃矿坑隐藏房间", "mod-i18n", "heuristic.mod");
            AssertContainsEntry(catalog, "location", "Custom_SDS.cliff", "废弃观景台", "mod-i18n", "heuristic.mod");
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void Build_ReadsStructuredLocationDisplayFiles()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(modDirectory, "i18n"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "assets", "DependencyData"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "code", "Locations"));

            File.WriteAllText(
                Path.Combine(modDirectory, "i18n", "zh.json"),
                "{\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_City.DisplayName\": \"祖祖城中心\",\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_Cavebase.DisplayName\": \"废弃矿坑\",\n" +
                "  \"SDS.CJBok.CheatsMenu.SDS_PeletteHouse.DisplayName\": \"佩莱特木屋\",\n" +
                "  \"DisplayName.GrampletonSuburbs\": \"格兰普顿郊区\",\n" +
                "  \"DisplayName.GrampletonSuburbsTrainStation\": \"格兰普顿郊区火车站\",\n" +
                "  \"LocationData.Grampleton_Fields\": \"格兰普顿平原\",\n" +
                "  \"LocationData.Grampleton_Train\": \"火车站\"\n" +
                "}"
            );

            File.WriteAllText(
                Path.Combine(modDirectory, "assets", "DependencyData", "CJBCheats.json"),
                "{\n" +
                "  \"Entries\": {\n" +
                "    \"demo.city\": {\n" +
                "      \"DisplayName\": \"{{i18n:SDS.CJBok.CheatsMenu.SDS_City.DisplayName}}\",\n" +
                "      \"Location\": \"Custom_SDS.City\"\n" +
                "    },\n" +
                "    \"demo.cave\": {\n" +
                "      \"DisplayName\": \"{{i18n:SDS.CJBok.CheatsMenu.SDS_Cavebase.DisplayName}}\",\n" +
                "      \"Location\": \"Custom_SDS.Cavebase\"\n" +
                "    },\n" +
                "    \"demo.pelette\": {\n" +
                "      \"DisplayName\": \"{{i18n:SDS.CJBok.CheatsMenu.SDS_PeletteHouse.DisplayName}}\",\n" +
                "      \"Location\": \"Custom_SDS.Phouse\"\n" +
                "    }\n" +
                "  }\n" +
                "}"
            );

            File.WriteAllText(
                Path.Combine(modDirectory, "code", "Locations", "CJBWarps.json"),
                "{\n" +
                "  \"Entries\": {\n" +
                "    \"demo.suburbs\": {\n" +
                "      \"DisplayName\": \"{{i18n:DisplayName.GrampletonSuburbs}}\",\n" +
                "      \"Location\": \"Custom_GrampletonSuburbs\"\n" +
                "    },\n" +
                "    \"demo.train\": {\n" +
                "      \"DisplayName\": \"{{i18n:DisplayName.GrampletonSuburbsTrainStation}}\",\n" +
                "      \"Location\": \"Custom_GrampletonSuburbsTrainStation\"\n" +
                "    }\n" +
                "  }\n" +
                "}"
            );

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "Structured Mod",
                UniqueID = "structured.mod"
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            AssertContainsEntry(catalog, "location", "Custom_SDS.City", "祖祖城中心", "mod-i18n", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_SDS.Cavebase", "废弃矿坑", "mod-i18n", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_SDS.Phouse", "佩莱特木屋", "structured-content", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_GrampletonSuburbs", "格兰普顿郊区", "mod-i18n", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_GrampletonSuburbsTrainStation", "格兰普顿郊区火车站", "mod-i18n", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_GrampletonFields", "格兰普顿平原", "mod-i18n", "structured.mod");
            AssertContainsEntry(catalog, "location", "Custom_GrampletonFields_Small", "格兰普顿平原", "mod-i18n", "structured.mod");
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void Build_ReadsWorldMapAndLocationsDataLocationDisplays()
    {
        var modDirectory = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(modDirectory, "i18n"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "code", "Locations"));

            File.WriteAllText(
                Path.Combine(modDirectory, "i18n", "zh.json"),
                "{\n" +
                "  \"Unused.Debug\": \"占位\"\n" +
                "}"
            );

            File.WriteAllText(
                Path.Combine(modDirectory, "code", "Locations", "WorldMap.json"),
                "{\n" +
                "  \"Entries\": {\n" +
                "    \"example.marnie\": {\n" +
                "      \"Id\": \"Custom_MarnieShed\",\n" +
                "      \"ScrollText\": \"玛妮的小屋\",\n" +
                "      \"WorldPositions\": [\n" +
                "        {\n" +
                "          \"LocationName\": \"Custom_MarnieShed\"\n" +
                "        }\n" +
                "      ]\n" +
                "    }\n" +
                "  }\n" +
                "}"
            );

            File.WriteAllText(
                Path.Combine(modDirectory, "code", "Locations", "LocationsData.json"),
                "{\n" +
                "  \"Changes\": [\n" +
                "    {\n" +
                "      \"Action\": \"EditData\",\n" +
                "      \"Target\": \"Data/Locations\",\n" +
                "      \"Entries\": {\n" +
                "        \"Custom_GrampletonSuburbsOutskirts\": {\n" +
                "          \"DisplayName\": \"格兰普顿郊区外围\",\n" +
                "          \"CreateOnLoad\": {\n" +
                "            \"MapPath\": \"Maps\\\\Custom_GrampletonSuburbsOutskirts\"\n" +
                "          }\n" +
                "        }\n" +
                "      }\n" +
                "    }\n" +
                "  ]\n" +
                "}"
            );

            var scannedMod = new ScannedMod
            {
                DirectoryPath = modDirectory,
                Name = "WorldMap Mod",
                UniqueID = "worldmap.mod"
            };

            var catalog = new TranslationCatalogBuilder().Build(new[] { scannedMod });

            AssertContainsEntry(catalog, "location", "Custom_MarnieShed", "玛妮的小屋", "structured-content", "worldmap.mod");
            AssertContainsEntry(catalog, "location", "Custom_GrampletonSuburbsOutskirts", "格兰普顿郊区外围", "structured-content", "worldmap.mod");
        }
        finally
        {
            Directory.Delete(modDirectory, recursive: true);
        }
    }

    private static void AssertContainsEntry(
        TranslationCatalog catalog,
        string category,
        string raw,
        string zh,
        string source,
        string? sourceModId = null)
    {
        var entry = catalog.Entries.FirstOrDefault(item =>
            string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Raw, raw, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SourceModId ?? string.Empty, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            throw new InvalidOperationException($"Expected translation entry was not found: {category} {raw} ({source}).");
        }

        if (!string.Equals(entry.Zh, zh, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Translation mismatch for {raw}. Expected '{zh}', actual '{entry.Zh}'.");
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "StardewStoryInspectorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
