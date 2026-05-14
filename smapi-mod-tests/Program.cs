using StardewStoryInspector.Tests;

if (args.Length > 0 && string.Equals(args[0], "rebuild-live-story-index", StringComparison.Ordinal))
{
    return LiveStoryIndexRebuildCommand.Run(args);
}

if (args.Length > 0 && string.Equals(args[0], "inspect-single-content-pack", StringComparison.Ordinal))
{
    return InspectSingleContentPackCommand.Run(args);
}

try
{
    LooseJsonParserTests.RunAll();
    RuntimeStoryStateRefreshServiceTests.RunAll();
    RuntimeStoryStateRefreshServiceHistoryTests.RunAll();
    StoryStateEvaluationExporterTests.RunAll();
    EventHistoryStoreTests.RunAll();
    EventHistoryTrackerTests.RunAll();
    StoryStateEvaluatorTests.RunAll();
    StoryNodeStatusClassifierTests.RunAll();
    ConditionEvaluatorTests.RunAll();
    RuntimeStateModelTests.RunAll();
    EventScriptChoiceIndexParserTests.RunAll();
    StoryNodeDialogueLinkerTests.RunAll();
    DialogueIndexParserTests.RunAll();
    EventKeySplitterTests.RunAll();
    EventPreconditionParserTests.RunAll();
    EventIndexBuilderTests.RunAll();
    TranslationCatalogBuilderTests.RunAll();
    Console.WriteLine("All tests passed.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
