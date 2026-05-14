using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Tests;

internal static class RuntimeStateModelTests
{
    public static void RunAll()
    {
        SharedMockRuntimeState_DeserializesIntoRuntimeStateModels();
    }

    private static void SharedMockRuntimeState_DeserializesIntoRuntimeStateModels()
    {
        var runtimeStatePath = Path.Combine(AppContext.BaseDirectory, "Shared", "mock-runtime-state.json");
        var rawJson = File.ReadAllText(runtimeStatePath);

        var export = JsonConvert.DeserializeObject<RuntimeStateExport>(rawJson)
            ?? throw new InvalidOperationException("Failed to deserialize mock-runtime-state.json into RuntimeStateExport.");

        AssertEqual("fall", export.State.Season, "RuntimeStateExport season mismatch.");
        AssertEqual(12, export.State.DayOfMonth, "RuntimeStateExport dayOfMonth mismatch.");
        AssertEqual("Friday", export.State.DayOfWeek, "RuntimeStateExport dayOfWeek mismatch.");
        AssertEqual(1900, export.State.Time, "RuntimeStateExport time mismatch.");
        AssertEqual("sunny", export.State.Weather, "RuntimeStateExport weather mismatch.");
        AssertEqual("Town", export.State.CurrentLocation, "RuntimeStateExport currentLocation mismatch.");
        AssertTrue(export.State.InstalledModIds.Contains("Pathoschild.ContentPatcher"), "RuntimeStateExport installedModIds should contain Content Patcher.");
        AssertEqual(2200, export.State.FriendshipPoints["Shane"], "RuntimeStateExport Shane friendship mismatch.");
        AssertTrue(export.State.SeenEvents.Contains("100001"), "RuntimeStateExport seenEvents should contain 100001.");
        AssertTrue(export.State.Mail.Contains("someMail"), "RuntimeStateExport mail should contain someMail.");
        AssertTrue(export.State.DialogueAnswers.Contains("ShaneAnswerA"), "RuntimeStateExport dialogueAnswers should contain ShaneAnswerA.");

        var stateToken = JObject.Parse(rawJson)["state"]
            ?? throw new InvalidOperationException("Mock runtime state JSON should contain a top-level state object.");
        var state = stateToken.ToObject<RuntimeGameState>()
            ?? throw new InvalidOperationException("Failed to deserialize nested state into RuntimeGameState.");

        AssertEqual("MockFarmer", state.PlayerName, "RuntimeGameState playerName mismatch.");
        AssertTrue(state.InstalledModIds.Contains("Pathoschild.ContentPatcher"), "RuntimeGameState installedModIds mismatch.");
        AssertEqual(1000, state.FriendshipPoints["Sam"], "RuntimeGameState Sam friendship mismatch.");
        AssertEqual("Hovsep", state.SpouseName, "RuntimeGameState spouseName mismatch.");
        AssertEqual("Hovsep", state.MarriedTo, "RuntimeGameState marriedTo mismatch.");
        AssertEqual("Sebastian", state.EngagedTo, "RuntimeGameState engagedTo mismatch.");
        AssertEqual("Krobus", state.Roommate, "RuntimeGameState roommate mismatch.");
        AssertTrue(state.Spouses is not null && state.Spouses.SequenceEqual(new[] { "Hovsep" }), "RuntimeGameState spouses mismatch.");
        AssertTrue(state.DatingNpcNames.SetEquals(new[] { "Sam" }), "RuntimeGameState datingNpcNames mismatch.");
        AssertTrue(state.DialogueAnswers.SetEquals(new[] { "ShaneAnswerA" }), "RuntimeGameState dialogueAnswers mismatch.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
