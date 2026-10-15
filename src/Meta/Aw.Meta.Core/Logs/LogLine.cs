using System.Globalization;

namespace Aw.Meta.Core.Logs;

/// <summary>The lines the meta appends to its running log, a markdown list.</summary>
public static class LogLine
{
    /// <summary>Reads a kind name, in any case.</summary>
    /// <param name="text">The name: merge, decision, lanes, incident or note.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>Whether the name is a kind.</returns>
    public static bool TryParseKind(string text, out LogKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Enum.TryParse(text, ignoreCase: true, out kind) && Enum.IsDefined(kind) && !char.IsAsciiDigit(text[0]);
    }

    /// <summary>Formats an event line: "- KIND yyyy-MM-dd HH:mm: text".</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="at">When it happened.</param>
    /// <param name="text">The text, on one line.</param>
    /// <returns>The line.</returns>
    public static string Event(LogKind kind, DateTimeOffset at, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"- {kind.ToString().ToUpperInvariant()} {at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}: {OneLine(text)}";
    }

    /// <summary>Formats the owner-decision line: "- OWNER (yyyy-MM-dd): text (asked by lane)".</summary>
    /// <param name="at">When the owner answered.</param>
    /// <param name="text">The answer.</param>
    /// <param name="askedBy">The asking lane's session.</param>
    /// <returns>The line.</returns>
    public static string Owner(DateTimeOffset at, string text, string askedBy)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"- OWNER ({at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}): {OneLine(text)} (asked by {askedBy})";
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
