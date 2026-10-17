using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>What the sample loop gathered.</summary>
/// <param name="Kept">The samples that were scored.</param>
/// <param name="Scores">Each running arm's sample scores.</param>
/// <param name="ExtractFailed">Samples dropped because git couldn't read their cut.</param>
/// <param name="NoBrief">Samples dropped because the source no longer had the record.</param>
internal sealed record Collected(IReadOnlyList<CorpusSample> Kept, Dictionary<Arm, List<SampleScore>> Scores, int ExtractFailed, int NoBrief)
{
    /// <summary>Model tokens per arm (empty with the adjudicator off).</summary>
    public Dictionary<Arm, int> Tokens { get; init; } = [];

    /// <summary>Samples dropped because the brief source was unavailable (SND-CONTEXT-011).</summary>
    public int SourceDown { get; init; }

    /// <summary>Kept samples whose as-of map was built (or cached) without a clusters pass, on a run whose arms read the map.</summary>
    public int WithoutClusters { get; init; }

    /// <summary>The code of the model error that stopped adjudication, or null.</summary>
    public string? AdjudicationError { get; init; }

    /// <summary>True when adjudication stopped at the token cap (SND-CONTEXT-083).</summary>
    public bool AdjudicationCapped { get; init; }
}
