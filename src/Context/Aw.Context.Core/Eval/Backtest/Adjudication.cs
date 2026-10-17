
namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What the adjudicator concluded.</summary>
/// <param name="Verdicts">One verdict per surface key.</param>
/// <param name="Tokens">The model tokens spent.</param>
/// <param name="Capped">True when the cap stopped it (SND-CONTEXT-083): the rest are <see cref="Verdict.Unjudged"/>.</param>
/// <param name="Error">The code of the model error that stopped it, or null: the surfaces are <see cref="Verdict.Unjudged"/> and the run's adjudication is incomplete.</param>
public sealed record Adjudication(IReadOnlyDictionary<string, Verdict> Verdicts, int Tokens, bool Capped, string? Error = null);
