namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Issues that did not become samples, by reason.</summary>
/// <param name="NoMerge">No merged PR carries the issue's key, or none has a cut (the issue predates the history).</param>
/// <param name="OutsideTeams">The key's team is not in 'context.sources.linear.teams'.</param>
/// <param name="ExtractFailed">The git read at the cut failed (SND-CONTEXT-048).</param>
/// <param name="Keys">The left-out keys with a reason, in key order.</param>
public sealed record LeftOut(int NoMerge, int OutsideTeams, int ExtractFailed, IReadOnlyList<string> Keys)
{
    /// <summary>Issues whose record the source no longer had when the brief was read (counted when the run reads them).</summary>
    public int NoBrief { get; init; }

    /// <summary>Issues whose brief could not be read because the source was down (SND-CONTEXT-011).</summary>
    public int SourceDown { get; init; }

    /// <summary>Nothing left out.</summary>
    public static LeftOut None { get; } = new(0, 0, 0, []);

    /// <summary>Everything left out.</summary>
    public int Total => NoMerge + OutsideTeams + ExtractFailed + NoBrief;
}
