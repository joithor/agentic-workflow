namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Turns issues and first-parent histories into samples (spec §9.1). Pure.</summary>
public static class CorpusBuilder
{
    /// <summary>Builds the corpus.</summary>
    /// <param name="issues">The candidate issues.</param>
    /// <param name="histories">The mapped repos' first-parent histories.</param>
    /// <param name="teams">The Linear team keys a key may name.</param>
    /// <param name="isDenied">Whether a path is under a deny glob.</param>
    /// <returns>The corpus.</returns>
    public static Corpus Build(IReadOnlyList<BriefIssue> issues, IReadOnlyList<RepoHistory> histories, IReadOnlyList<string> teams, Func<string, bool> isDenied) =>
        throw new NotSupportedException();

    /// <summary>Whether <paramref name="text"/> carries <paramref name="key"/> as a whole token.</summary>
    /// <param name="text">A merge title or branch.</param>
    /// <param name="key">The issue key.</param>
    /// <returns>True when it does.</returns>
    public static bool Carries(string? text, string key) => throw new NotSupportedException();
}
