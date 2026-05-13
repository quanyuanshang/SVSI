using StardewStoryInspector.Models;

namespace StardewStoryInspector.Services;

public sealed class EventKeySplitter
{
    public EventKeySplitResult Split(string rawEventKey)
    {
        var segments = new List<string>();
        var current = new System.Text.StringBuilder(rawEventKey.Length);
        var inQuotes = false;
        var isEscaped = false;
        var sawSeparator = false;

        foreach (var ch in rawEventKey)
        {
            if (isEscaped)
            {
                current.Append(ch);
                isEscaped = false;
                continue;
            }

            if (ch == '\\' && inQuotes)
            {
                current.Append(ch);
                isEscaped = true;
                continue;
            }

            if (ch == '"')
            {
                current.Append(ch);
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == '/' && !inQuotes)
            {
                segments.Add(current.ToString());
                current.Clear();
                sawSeparator = true;
                continue;
            }

            current.Append(ch);
        }

        if (!sawSeparator)
        {
            return new EventKeySplitResult
            {
                EventId = rawEventKey,
                Warnings =
                {
                    "Raw event key does not contain a slash separator; treating the full key as eventId."
                }
            };
        }

        segments.Add(current.ToString());

        return new EventKeySplitResult
        {
            EventId = segments[0],
            PreconditionFragments = segments.Skip(1).ToList()
        };
    }
}
