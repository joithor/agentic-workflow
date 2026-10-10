using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Ids;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// Asks one question (the TS evaluate()): a disabled question or an invalid input escalates, a pre-rule settles, else
/// the chain is walked until a provider decides above threshold or a fallback rule settles a below-threshold answer.
/// Every evaluation writes one decision row; storage failures never change the outcome.
/// </summary>
/// <param name="log">Where decisions and failures go.</param>
/// <param name="redactor">Strips secrets from the stored input.</param>
/// <param name="ids">Makes the decision id.</param>
/// <param name="clock">Timestamps and latencies.</param>
public sealed class QuestionEvaluator(IDecisionLog log, IInputRedactor redactor, IIdGenerator ids, TimeProvider clock)
{
    /// <summary>Evaluates a question against a raw input.</summary>
    /// <typeparam name="TInput">The question's validated input.</typeparam>
    /// <param name="question">The question.</param>
    /// <param name="rawInput">The input as read (JSON null as null).</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public ValueTask<EvaluationOutcome> Evaluate<TInput>(
        QuestionDefinition<TInput> question, JsonNode? rawInput, EvaluationContext context, CancellationToken cancellationToken)
        where TInput : class
        => throw new NotSupportedException(
            $"red: evaluate {question?.Name} on {rawInput} in {context} ({cancellationToken.IsCancellationRequested}) with {log}, {redactor}, {ids}, {clock}");
}
