using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Scores one answer against one sample (spec §9.1, plan decision 5). Pure.</summary>
public static class BacktestScoring
{
    /// <summary>The truth nodes in the as-of map.</summary>
    /// <param name="asOf">The as-of graph.</param>
    /// <param name="truthFiles">The changed files.</param>
    /// <returns>Node keys.</returns>
    public static IReadOnlyList<string> TruthNodes(GraphSnapshot asOf, IReadOnlyList<string> truthFiles) => throw new NotSupportedException();

    /// <summary>The nodes an answer named.</summary>
    /// <param name="result">The result.</param>
    /// <returns>Node keys.</returns>
    public static IReadOnlyList<string> PredictedNodes(ContextResult result) => throw new NotSupportedException();

    /// <summary>The files an answer pointed at.</summary>
    /// <param name="result">The result.</param>
    /// <returns>Paths.</returns>
    public static IReadOnlyList<string> PredictedFiles(ContextResult result) => throw new NotSupportedException();

    /// <summary>Scores an answer.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="arm">The arm name.</param>
    /// <param name="truthNodes">Truth nodes.</param>
    /// <param name="result">The answer.</param>
    /// <param name="armLeaky">Whether the arm is leaky.</param>
    /// <returns>The score.</returns>
    public static SampleScore Score(CorpusSample sample, string arm, IReadOnlyList<string> truthNodes, ContextResult result, bool armLeaky) => throw new NotSupportedException();

    /// <summary>One arm's means.</summary>
    /// <param name="scores">The scores.</param>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The means.</returns>
    public static ArmScore Aggregate(IReadOnlyList<SampleScore> scores, int tokens) => throw new NotSupportedException();
}
