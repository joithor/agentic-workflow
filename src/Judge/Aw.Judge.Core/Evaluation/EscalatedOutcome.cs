using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Evaluation;

/// <summary>Nothing decided; the caller escalates to the human or its default.</summary>
/// <param name="ReasonCode">'question-disabled', 'invalid-input', 'below-threshold' or 'no-provider-decided'.</param>
public sealed record EscalatedOutcome(string ReasonCode) : EvaluationOutcome(ReasonCode)
{
    /// <summary>The recorded decision's id; set only for a question that reports its trace (choose).</summary>
    public DecisionId? Id { get; init; }

    /// <summary>The probabilities a provider sent before the call fell below threshold; set only for a question that reports its trace.</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }
}
