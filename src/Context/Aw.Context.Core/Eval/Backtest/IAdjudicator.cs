namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Labels surfaces no PR touched as relevant to a brief or not, through the model port (spec §9.1).</summary>
public interface IAdjudicator
{
    /// <summary>Judges the surfaces.</summary>
    /// <param name="request">The brief, the surfaces and the token room left.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A verdict per surface, the tokens spent and whether the cap stopped it.</returns>
    public ValueTask<Adjudication> Judge(AdjudicationRequest request, CancellationToken cancellationToken);
}
