using System.Text.RegularExpressions;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Turns issues and first-parent histories into samples (spec §9.1). Pure.</summary>
public static partial class CorpusBuilder
{
    /// <summary>Builds the corpus.</summary>
    /// <param name="issues">The candidate issues.</param>
    /// <param name="histories">The mapped repos' first-parent histories.</param>
    /// <param name="teams">The Linear team keys a key may name ('context.sources.linear.teams').</param>
    /// <param name="isDenied">Whether a path is under a deny glob (left out of the truth).</param>
    /// <returns>The samples (the earliest merge carrying a key), and the issues left out.</returns>
    public static Corpus Build(IReadOnlyList<BriefIssue> issues, IReadOnlyList<RepoHistory> histories, IReadOnlyList<string> teams, Func<string, bool> isDenied)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(histories);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(isDenied);
        var samples = new List<CorpusSample>();
        var left = new List<(string Key, bool OutsideTeam)>();
        foreach (var issue in issues.OrderBy(i => i.Key, StringComparer.Ordinal))
        {
            var team = issue.Key[..Math.Max(0, issue.Key.LastIndexOf('-'))];
            if (!teams.Contains(team, StringComparer.Ordinal))
            {
                left.Add((issue.Key, true));
                continue;
            }

            var sample = histories.Select(h => Sample(issue, h, isDenied)).OfType<Found>().OrderBy(f => f.MergedAt).Select(f => f.Sample).FirstOrDefault();   // the earliest merge across the repos
            if (sample is null)
            {
                left.Add((issue.Key, false));
            }
            else
            {
                samples.Add(sample);
            }
        }

        return new Corpus(samples, new LeftOut(left.Count(l => !l.OutsideTeam), left.Count(l => l.OutsideTeam), 0, [.. left.Select(l => l.Key)]));
    }

    /// <summary>Whether <paramref name="text"/> carries <paramref name="key"/> as a whole token ('SPL-10' is not in 'SPL-101').</summary>
    /// <param name="text">A merge title or branch.</param>
    /// <param name="key">The issue key.</param>
    /// <returns>True when it does.</returns>
    public static bool Carries(string? text, string key) =>
        text is not null && Keys(text).Contains(key, StringComparer.OrdinalIgnoreCase);

    // A merge at or before the moment the issue existed is no sample: its cut would already hold the answer, so it could only score as clean what it was given.
    // The cut is the last commit before the merge (in first-parent order) at or before the issue's creation.
    private static Found? Sample(BriefIssue issue, RepoHistory history, Func<string, bool> isDenied)
    {
        var at = history.Commits.ToList().FindIndex(c => c.IsMerge && (Carries(c.Subject, issue.Key) || Carries(c.Branch, issue.Key)));
        if (at < 0 || history.Commits[at].At <= issue.CreatedAt)
        {
            return null;
        }

        var merge = history.Commits[at];
        var cut = history.Commits.Take(at).LastOrDefault(c => c.At <= issue.CreatedAt);
        var truth = merge.ChangedFiles.Where(f => !isDenied(f)).Order(StringComparer.Ordinal).ToList();
        return cut is null || truth.Count == 0
            ? null
            : new Found(new CorpusSample(issue.Key, issue.RecordRef, issue.Version, issue.CreatedAt, issue.UpdatedAt > issue.CreatedAt, history.Repo, merge.Sha, cut.Sha, truth), merge.At);
    }

    private sealed record Found(CorpusSample Sample, DateTimeOffset MergedAt);

    private static IEnumerable<string> Keys(string text) => KeyShape.Matches(text).Select(m => m.Value);

    [GeneratedRegex("(?<![A-Za-z0-9])[A-Z][A-Z0-9]{1,9}-[0-9]{1,7}(?![A-Za-z0-9])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyShape { get; }
}
