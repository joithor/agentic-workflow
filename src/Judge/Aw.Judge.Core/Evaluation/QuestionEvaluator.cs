using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;

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
/// <param name="json">The package's AwJson options, for the digest and the stored input.</param>
public sealed partial class QuestionEvaluator(IDecisionLog log, IInputRedactor redactor, IIdGenerator ids, TimeProvider clock, JsonSerializerOptions json)
{
    /// <summary>Evaluates a question against a raw input.</summary>
    /// <typeparam name="TInput">The question's validated input.</typeparam>
    /// <param name="question">The question.</param>
    /// <param name="rawInput">The input text as read (stdin); anything that doesn't deserialize into the question's input record, syntax or shape, escalates as 'invalid-input'.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public async ValueTask<EvaluationOutcome> Evaluate<TInput>(
        QuestionDefinition<TInput> question, string rawInput, EvaluationContext context, CancellationToken cancellationToken)
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawInput);
        var run = new EvaluationRun(DecisionId.From(ids.NewId().ToString()), clock.GetUtcNow(), RawNode(rawInput)) { SessionId = context.SessionId };
        var settings = context.Override;
        if (settings is { Enabled: false })
        {
            return await Escalate(question, run, EvaluationReasons.QuestionDisabled, DecisionOutcome.Escalated, cancellationToken).ConfigureAwait(false);
        }

        var parsed = AwJson.Deserialize<TInput>(Encoding.UTF8.GetBytes(rawInput), json);
        if (parsed.IsError)
        {
            return await Escalate(question, run, EvaluationReasons.InvalidInput, DecisionOutcome.Escalated, cancellationToken).ConfigureAwait(false);
        }

        var input = parsed.Value;
        run.Input = JsonNode.Parse(AwJson.Serialize(input, json));
        run.Threshold = settings?.Threshold ?? question.Threshold;
        if (question.PreRules?.Invoke(input) is { } preRule)
        {
            var settlement = Rule(preRule, EvaluationReasons.PreRule, latencyMs: 0, chainPosition: 0);
            return await Settle(question, run, settlement, extra: null, cancellationToken).ConfigureAwait(false);
        }

        var candidates = ChainRouting.Candidates(context.Chain, question.ContentClass, context.Providers, settings?.Providers);
        return await Walk(question, input, candidates, run, cancellationToken).ConfigureAwait(false);
    }

    // What escalation rows digest and store: the parsed JSON, or the text itself as a JSON string when it isn't JSON.
    private static JsonNode? RawNode(string rawInput)
    {
        try
        {
            return JsonNode.Parse(rawInput);
        }
        catch (JsonException)
        {
            return JsonValue.Create(rawInput);
        }
    }

    private static Settlement Rule(string decision, string reasonCode, long latencyMs, int chainPosition) => new()
    {
        Outcome = DecisionOutcome.Decided,
        Decision = decision,
        Confidence = 1,
        Provider = ProviderName.Rules,
        ReasonCode = reasonCode,
        LatencyMs = latencyMs,
        ChainPosition = chainPosition,
    };

    // Fallback rules are advisory: a throwing rule is "no opinion" and never escapes the evaluation (fails open).
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A question's fallback rule is advisory; any throw means no opinion (TS evaluate safeFallback).")]
    private static string? SafeFallback<TInput>(QuestionDefinition<TInput> question, TInput input)
        where TInput : class
    {
        try
        {
            return question.FallbackRules?.Invoke(input);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async ValueTask<EvaluationOutcome> Walk<TInput>(
        QuestionDefinition<TInput> question, TInput input, IReadOnlyList<IModelProvider> candidates, EvaluationRun run,
        CancellationToken cancellationToken)
        where TInput : class
    {
        var reference = question.ToRef(input);
        for (var i = 0; i < candidates.Count; i++)
        {
            var provider = candidates[i];
            var start = clock.GetTimestamp();
            var result = await Ask(provider, reference, run.Input, question.TimeBudget, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                await Fail(question.Name, provider.Name, EvaluationReasons.ProviderThrew, cancellationToken).ConfigureAwait(false);
                run.Skip(provider.Name, SkipReason.Failed);
                continue;
            }

            var latencyMs = (long)clock.GetElapsedTime(start).TotalMilliseconds;
            var answer = new ProviderAnswer(provider.Name, i, latencyMs, result);
            var settlement = await Step(question, input, run, answer, cancellationToken).ConfigureAwait(false);
            if (settlement is not null)
            {
                return await Settle(question, run, settlement, (result as ProviderDecided)?.Extra, cancellationToken).ConfigureAwait(false);
            }
        }

        var outcome = run.HadRealFailure ? DecisionOutcome.Failed : DecisionOutcome.Escalated;
        return await Escalate(question, run, run.LastReason, outcome, cancellationToken).ConfigureAwait(false);
    }

    // One provider's answer: a settlement when it ends the walk, null to try the next provider.
    private async ValueTask<Settlement?> Step<TInput>(
        QuestionDefinition<TInput> question, TInput input, EvaluationRun run, ProviderAnswer at, CancellationToken cancellationToken)
        where TInput : class
    {
        var result = at.Result;
        switch (StepVerdicts.Of(result, question.Outputs, run.Threshold))
        {
            case StepVerdict.Unavailable:
                run.Skip(at.Name, SkipReason.Unavailable);
                return null;
            case StepVerdict.TimedOut:
                await Fail(question.Name, at.Name, ProviderUnavailable.Timeout, cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Timeout);
                return null;
            case StepVerdict.Errored:
            case StepVerdict.OutOfEnum:
                var code = result is ProviderDecided ? EvaluationReasons.OutOfEnum : result.ReasonCode;
                await Fail(question.Name, at.Name, code, cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Failed);
                return null;
            case StepVerdict.BelowThreshold:
                run.Undecided(at.Name, (ProviderDecided)result);
                return SafeFallback(question, input) is { } fallback
                    ? Rule(fallback, EvaluationReasons.FallbackAfterUndecided, at.LatencyMs, at.Position) with
                    {
                        Probabilities = run.UndecidedProbabilities,
                        Agreement = Agreement.Undecided,
                    }
                    : null;
            case StepVerdict.Decided:
            default:
                return Decided(question, input, run, at, (ProviderDecided)result);
        }
    }

    private static Settlement Decided<TInput>(
        QuestionDefinition<TInput> question, TInput input, EvaluationRun run, ProviderAnswer at,
        ProviderDecided answer)
        where TInput : class
    {
        // What the fallback rules would have said: telemetry only, never a change to the model's decision.
        var opinion = SafeFallback(question, input);
        Agreement? agreement = opinion is null ? null
            : string.Equals(opinion, answer.Decision, StringComparison.Ordinal) ? Agreement.Agreed : Agreement.Overrode;
        return new Settlement
        {
            Outcome = DecisionOutcome.Decided,
            Decision = answer.Decision,
            Confidence = answer.Confidence,
            Provider = at.Name,
            ReasonCode = answer.ReasonCode,
            LatencyMs = at.LatencyMs,
            ChainPosition = at.Position,
            Probabilities = answer.Probabilities ?? run.UndecidedProbabilities,
            RulesOpinion = opinion,
            Agreement = agreement,
        };
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A provider that throws is a recorded 'provider-threw' failure and the chain moves on (TS evaluate); cancellation still propagates.")]
    private static async ValueTask<ProviderResult?> Ask(
        IModelProvider provider, QuestionRef reference, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.Decide(reference, input, budget, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
