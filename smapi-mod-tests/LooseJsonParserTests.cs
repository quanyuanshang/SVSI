using System.Text.Json.Nodes;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class LooseJsonParserTests
{
    public static void RunAll()
    {
        ParseNodeFromText_AllowsApostrophesInsideComments();
    }

    private static void ParseNodeFromText_AllowsApostrophesInsideComments()
    {
        var json =
            "{\n" +
            "  \"Changes\": [\n" +
            "    //WHAT'S UP INVITE\n" +
            "    {\n" +
            "      \"LogName\": \"What's Up Invite\",\n" +
            "      \"Action\": \"EditData\",\n" +
            "      \"Target\": \"Data/Events/Farm\",\n" +
            "      \"Entries\": {\n" +
            "        \"SomeEvent/t 600 800\": \"continue/end\"\n" +
            "      }\n" +
            "    }\n" +
            "  ]\n" +
            "}";

        var node = LooseJsonParser.ParseNodeFromText(json) as JsonObject
            ?? throw new Exception("Expected parsed JSON object.");

        var changes = node["Changes"] as JsonArray
            ?? throw new Exception("Expected Changes array.");

        if (changes.Count != 1)
        {
            throw new Exception($"Expected 1 change, found {changes.Count}.");
        }

        var change = changes[0] as JsonObject
            ?? throw new Exception("Expected first change object.");

        var logName = change["LogName"]?.GetValue<string>();
        if (!string.Equals(logName, "What's Up Invite", StringComparison.Ordinal))
        {
            throw new Exception($"Expected LogName to round-trip, found '{logName}'.");
        }
    }
}
