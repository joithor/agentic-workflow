using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>The corpus and the samples in the request's window.</summary>
/// <param name="Corpus">The corpus (eligible samples and what was left out).</param>
/// <param name="Samples">The samples in the window, earliest first.</param>
/// <param name="Issues">The listed issues by key (a brief is read by its issue).</param>
internal sealed record LoadedCorpus(Corpus Corpus, IReadOnlyList<CorpusSample> Samples, Dictionary<string, BriefIssue> Issues)
{
    /// <summary>The teams whose issue listing hit the source's cap: issues past it are not in the corpus.</summary>
    public IReadOnlyList<string> CappedTeams { get; init; } = [];
}
