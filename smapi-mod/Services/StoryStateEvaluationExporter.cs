using System.Text.Json;
using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class StoryStateEvaluationExporter
{
    private readonly StoryStateEvaluator storyStateEvaluator;

    public StoryStateEvaluationExporter()
        : this(new StoryStateEvaluator())
    {
    }

    public StoryStateEvaluationExporter(StoryStateEvaluator storyStateEvaluator)
    {
        this.storyStateEvaluator = storyStateEvaluator;
    }

    public List<StoryNode> LoadStoryNodesFromFile(string storyIndexPath)
    {
        var resolvedPath = EnsureFileExists(storyIndexPath, "Story index");

        try
        {
            var index = JsonSerializer.Deserialize<StoryRawEventIndex>(
                File.ReadAllText(resolvedPath),
                JsonExportOptions.Default
            );

            if (index is null)
            {
                throw new InvalidOperationException($"Story index file '{resolvedPath}' was empty or could not be deserialized.");
            }

            return index.Nodes;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse story index file '{resolvedPath}': {ex.Message}", ex);
        }
    }

    public RuntimeGameState LoadRuntimeStateFromFile(string runtimeStatePath)
    {
        var resolvedPath = EnsureFileExists(runtimeStatePath, "Runtime state");
        var json = File.ReadAllText(resolvedPath);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("state", out _))
        {
            try
            {
                var wrappedState = JsonSerializer.Deserialize<RuntimeStateExport>(json, JsonExportOptions.Default);
                if (wrappedState is not null)
                {
                    return wrappedState.State;
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse runtime state file '{resolvedPath}': {ex.Message}", ex);
            }
        }

        try
        {
            var directState = JsonSerializer.Deserialize<RuntimeGameState>(json, JsonExportOptions.Default);
            if (directState is null)
            {
                throw new InvalidOperationException($"Runtime state file '{resolvedPath}' was empty or could not be deserialized.");
            }

            return directState;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse runtime state file '{resolvedPath}': {ex.Message}", ex);
        }
    }

    public StoryStateEvaluationReport EvaluateAndWrite(
        string storyIndexPath,
        string runtimeStatePath,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        var nodes = this.LoadStoryNodesFromFile(storyIndexPath);
        var state = this.LoadRuntimeStateFromFile(runtimeStatePath);
        var report = this.storyStateEvaluator.Evaluate(nodes, state);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonExportOptions.Default));
        return report;
    }

    private static string EnsureFileExists(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{label} path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label} file was not found: {path}", path);
        }

        return path;
    }
}
