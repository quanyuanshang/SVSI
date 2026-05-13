using System.Text.Json;
using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class StoryStateEvaluationExporterTests
{
    public static void RunAll()
    {
        EvaluateAndWrite_WritesExpectedReportFile();
    }

    private static void EvaluateAndWrite_WritesExpectedReportFile()
    {
        var exporter = new StoryStateEvaluationExporter();
        var baseDirectory = AppContext.BaseDirectory;
        var storyIndexPath = Path.Combine(baseDirectory, "TestData", "story-index.linked.mock.json");
        var runtimeStatePath = Path.Combine(baseDirectory, "TestData", "mock-runtime-state.mock.json");
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "StardewStoryInspector.Tests",
            nameof(StoryStateEvaluationExporterTests),
            Guid.NewGuid().ToString("N")
        );
        var outputPath = Path.Combine(outputDirectory, "story-state.evaluated.mock.json");

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var report = exporter.EvaluateAndWrite(storyIndexPath, runtimeStatePath, outputPath);

        AssertTrue(File.Exists(outputPath), "Exporter should write the output file.");
        AssertEqual(2, report.TotalNodeCount, "Exporter should evaluate both mock nodes.");

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        AssertTrue(document.RootElement.TryGetProperty("statusCounts", out var statusCounts), "Output JSON should include statusCounts.");
        AssertTrue(document.RootElement.TryGetProperty("nodes", out var nodes), "Output JSON should include nodes.");
        AssertTrue(statusCounts.ValueKind == JsonValueKind.Object, "statusCounts should be a JSON object.");
        AssertTrue(nodes.ValueKind == JsonValueKind.Array, "nodes should be a JSON array.");
        AssertTrue(nodes.GetArrayLength() == 2, "nodes array should contain two evaluated entries.");
        var firstNode = nodes[0];
        AssertTrue(firstNode.TryGetProperty("status", out var status), "Each node should include a status field.");
        AssertEqual(JsonValueKind.String, status.ValueKind, "Status should serialize as a JSON string.");
        var rawJson = File.ReadAllText(outputPath);
        AssertTrue(
            rawJson.Contains("\"status\":\"Current\"", StringComparison.Ordinal) ||
            rawJson.Contains("\"status\": \"Current\"", StringComparison.Ordinal),
            "Output JSON should contain a string enum status."
        );
        AssertTrue(statusCounts.TryGetProperty("Current", out _), "statusCounts should include Current.");
        AssertTrue(statusCounts.TryGetProperty("Locked", out _), "statusCounts should include Locked.");
        AssertTrue(statusCounts.TryGetProperty("AvailableLater", out _), "statusCounts should include AvailableLater.");
        AssertTrue(statusCounts.TryGetProperty("Triggered", out _), "statusCounts should include Triggered.");
        AssertTrue(statusCounts.TryGetProperty("Unknown", out _), "statusCounts should include Unknown.");
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
