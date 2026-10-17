using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Builds the eligible samples: the issues the brief source lists, the mapped repos' merges, and the window (spec §9.1).</summary>
internal sealed class CorpusLoader(IBriefSource briefs, IMergeHistory history)
{
    /// <summary>Loads the corpus.</summary>
    /// <param name="request">The window and limit.</param>
    /// <param name="world">The settings, repos and scrubbers of the call.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The corpus, the samples in the window, and the issues by key; SND-CONTEXT-080 with none, or the source's or git's error.</returns>
    public async ValueTask<Result<LoadedCorpus>> Load(BacktestRequest request, BriefScope world, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(world);
        var issues = await briefs.Issues(world, request.Since, cancellationToken).ConfigureAwait(false);
        if (issues.IsError)
        {
            return issues.Error;
        }

        var histories = new List<RepoHistory>();
        foreach (var repo in world.Repos)
        {
            var one = await history.History(repo, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            histories.Add(one.Value);
        }

        var corpus = CorpusBuilder.Build(issues.Value.Issues, histories, world.Settings.LinearTeams, path => world.Repos.Any(r => r.Denies(path)));
        var samples = Window(corpus.Samples, request);
        return samples.Count == 0
            ? NoSample(corpus, issues.Value)
            : new LoadedCorpus(corpus, samples, issues.Value.Issues.ToDictionary(i => i.Key, StringComparer.Ordinal)) { CappedTeams = issues.Value.CappedTeams };
    }

    private static List<CorpusSample> Window(IReadOnlyList<CorpusSample> samples, BacktestRequest request) =>
    [
        .. samples.Where(s => (request.Since is null || s.CreatedAt >= request.Since) && (request.Until is null || s.CreatedAt <= request.Until))
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.IssueKey, StringComparer.Ordinal).Take(request.Limit ?? int.MaxValue),
    ];

    private static Error NoSample(Corpus corpus, BriefListing listing) =>
        ContextErrors.NoEligibleSample.With(string.Create(CultureInfo.InvariantCulture,
            $"no eligible backtest sample: {listing.Issues.Count} issues listed{(listing.CappedTeams.Count > 0 ? $" (listing capped: {string.Join(", ", listing.CappedTeams)}; the source has no next page, so more may exist)" : string.Empty)}, {corpus.Samples.Count} with a merged PR, none in the window; left out: {corpus.LeftOut.NoMerge} with no merged PR, {corpus.LeftOut.OutsideTeams} outside the profile's teams (fix: widen --since/--until, or list the team in context.sources.linear.teams)"));
}
