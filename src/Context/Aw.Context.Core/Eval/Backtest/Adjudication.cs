
namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What the adjudicator concluded.</summary>
/// <param name="Verdicts">One verdict per surface key.</param>
/// <param name="Tokens">The model tokens spent.</param>
/// <param name="Capped">True when the cap stopped it (SND-CONTEXT-083): the rest are <see cref="Verdict.Unjudged"/>.</param>
public sealed record Adjudication(IReadOnlyDictionary<string, Verdict> Verdicts, int Tokens, bool Capped);
