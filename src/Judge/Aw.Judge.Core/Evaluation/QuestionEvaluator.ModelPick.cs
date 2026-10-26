
namespace Aw.Judge.Core.Evaluation;

/// <summary>model-pick's entry: its definition comes from the input, the pack filters run before the chain, and the chain is fixed.</summary>
public sealed partial class QuestionEvaluator
{
    /// <summary>Evaluates a model-pick call.</summary>
    /// <param name="rawInput">The input text as read; input that doesn't deserialize escalates as 'invalid-input'.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public ValueTask<EvaluationOutcome> EvaluateModelPick(string rawInput, EvaluationContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("model-pick is not built yet");
}
