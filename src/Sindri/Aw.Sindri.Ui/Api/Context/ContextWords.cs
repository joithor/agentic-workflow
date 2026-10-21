namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// The words the context routes serve, as strings (a new served word never makes the page unreadable) with the ones this
/// build knows listed here. <see cref="Known"/> says whether a word is one of them; a caller draws any other word raw.
/// </summary>
internal static class ContextWords
{
    /// <summary>A review entry's group.</summary>
    internal static IReadOnlyList<string> Groups { get; } = ["deterministic", "model", "orphaned"];

    /// <summary>A file map entry's status.</summary>
    internal static IReadOnlyList<string> FileStatuses { get; } = ["mapped", "suggested", "unmapped"];

    /// <summary>A file map entry's type.</summary>
    internal static IReadOnlyList<string> EntryTypes { get; } = ["dir", "file"];

    /// <summary>A conflict's kind.</summary>
    internal static IReadOnlyList<string> ConflictKinds { get; } = ["supersedes", "contradicts"];

    /// <summary>The symbol lens's state.</summary>
    internal static IReadOnlyList<string> SymbolStates { get; } = ["unavailable", "stale", "ok"];

    /// <summary>A node's or record's trust.</summary>
    internal static IReadOnlyList<string> Trusts { get; } = ["trusted", "untrusted"];

    /// <summary>A source's state on the Sources panel.</summary>
    internal static IReadOnlyList<string> SourceStates { get; } = ["ok", "degraded", "unavailable"];

    /// <summary>A searched source's state in an answer.</summary>
    internal static IReadOnlyList<string> SearchedStates { get; } = ["ok", "degraded", "unavailable", "not-configured"];

    /// <summary>Whether a word is one this build knows.</summary>
    /// <param name="words">The word set.</param>
    /// <param name="word">The word as served.</param>
    /// <returns>True when the set lists it.</returns>
    internal static bool Known(IReadOnlyList<string> words, string? word) => word is not null && words.Contains(word, StringComparer.Ordinal);
}
