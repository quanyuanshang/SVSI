using StardewStoryInspector.Services;

namespace StardewStoryInspector.Tests;

internal static class EventKeySplitterTests
{
    public static void RunAll()
    {
        Split_SeparatesEventIdAndPreconditions();
        Split_HandlesTrailingSlash();
        Split_DoesNotSplitQuotedSlash();
        Split_NoSlash_ReturnsWarning();
    }

    private static void Split_SeparatesEventIdAndPreconditions()
    {
        var result = new EventKeySplitter().Split("123/f Shane 2000/Time 1800 2200");

        AssertEqual("123", result.EventId, "EventId mismatch for basic split.");
        AssertSequenceEqual(
            new[] { "f Shane 2000", "Time 1800 2200" },
            result.PreconditionFragments,
            "Precondition fragments mismatch for basic split."
        );
        AssertEqual(0, result.Warnings.Count, "Basic split should not emit warnings.");
    }

    private static void Split_HandlesTrailingSlash()
    {
        var result = new EventKeySplitter().Split("abc.Event/");

        AssertEqual("abc.Event", result.EventId, "EventId mismatch for trailing slash.");
        AssertSequenceEqual(
            new[] { string.Empty },
            result.PreconditionFragments,
            "Trailing slash should preserve an empty trailing fragment."
        );
        AssertEqual(0, result.Warnings.Count, "Trailing slash should not emit warnings.");
    }

    private static void Split_DoesNotSplitQuotedSlash()
    {
        var result = new EventKeySplitter().Split("abc.Event/GameStateQuery \"A / B\"/Season spring");

        AssertEqual("abc.Event", result.EventId, "EventId mismatch for quoted slash.");
        AssertSequenceEqual(
            new[] { "GameStateQuery \"A / B\"", "Season spring" },
            result.PreconditionFragments,
            "Quoted slash should stay inside the same fragment."
        );
        AssertEqual(0, result.Warnings.Count, "Quoted slash split should not emit warnings.");
    }

    private static void Split_NoSlash_ReturnsWarning()
    {
        var result = new EventKeySplitter().Split("noSlashKey");

        AssertEqual("noSlashKey", result.EventId, "No-slash key should use the full key as eventId.");
        AssertEqual(0, result.PreconditionFragments.Count, "No-slash key should not have precondition fragments.");
        AssertEqual(1, result.Warnings.Count, "No-slash key should emit a warning.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected: [{string.Join(", ", expected)}]; Actual: [{string.Join(", ", actual)}]"
            );
        }
    }
}
