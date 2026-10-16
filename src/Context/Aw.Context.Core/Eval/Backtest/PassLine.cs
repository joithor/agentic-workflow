namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The §9.4 pass line for one arm. Pure.</summary>
public static class PassLine
{
    /// <summary>Judges an arm.</summary>
    /// <param name="arm">The arm's means.</param>
    /// <param name="brief">The brief-only arm's means, or null.</param>
    /// <param name="sampleCount">The corpus size.</param>
    /// <returns>The bars and the overall line.</returns>
    public static ArmPassLine Evaluate(ArmScore arm, ArmScore? brief, int sampleCount) => throw new NotSupportedException();
}
