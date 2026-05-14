namespace StardewStoryInspector.Models;

public sealed class TranslationCatalog
{
    public List<TranslationEntry> Entries { get; init; } = new();

    public List<TranslationWarning> Warnings { get; init; } = new();
}

public sealed class TranslationEntry
{
    public string Category { get; init; } = string.Empty;

    public string Raw { get; init; } = string.Empty;

    public string Zh { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string? SourceModId { get; init; }

    public string? SourceModName { get; init; }

    public string? SourcePath { get; init; }
}

public sealed class TranslationWarning
{
    public string Message { get; init; } = string.Empty;

    public string? SourceModId { get; init; }

    public string? SourceModName { get; init; }

    public string? SourcePath { get; init; }
}
