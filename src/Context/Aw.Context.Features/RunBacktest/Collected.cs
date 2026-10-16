using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>What the sample loop gathered.</summary>
/// <param name="Kept">The samples that were scored.</param>
/// <param name="Scores">Each running arm's sample scores.</param>
/// <param name="ExtractFailed">Samples dropped because git couldn't read their cut.</param>
/// <param name="NoBrief">Samples dropped because the source no longer had the record.</param>
internal sealed record Collected(IReadOnlyList<CorpusSample> Kept, Dictionary<Arm, List<SampleScore>> Scores, int ExtractFailed, int NoBrief);
