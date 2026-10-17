namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What the adjudicator concluded about one surface no PR touched.</summary>
public enum Verdict
{
    /// <summary>Both runs (in reversed order) said it is relevant to the brief.</summary>
    Relevant,

    /// <summary>Both runs said it is not.</summary>
    NotRelevant,

    /// <summary>The runs disagreed or one gave no valid answer: counted as not relevant.</summary>
    Unstable,

    /// <summary>The token cap was reached before it was asked.</summary>
    Unjudged,
}
