using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.Adjudicate;

/// <summary>
/// A stronger model labels stored inputs: <see cref="Samples"/> asks per item, a label only on a strict majority that is
/// one of the question's outputs (a split, an off-enum answer or an errored run stores nothing). The model sees the
/// question's own prompt, every option's criteria and the input (redacted first: the stored text is untrusted), never the
/// decision being scored (<see cref="EvalItem.ModelDecision"/>). The provider is the catalog's adjudicator, whose child runs
/// with no tools, settings or hooks (ClaudeCliProvider).
/// </summary>
/// <param name="store">Where the items and labels are.</param>
/// <param name="catalog">Hands out the adjudicator.</param>
/// <param name="redactor">Strips secrets from the input before the prompt is built.</param>
/// <param name="clock">Stamps the labels.</param>
/// <param name="json">The package's AwJson options.</param>
internal sealed class AdjudicateService(IEvalStore store, IProviderCatalog catalog, IInputRedactor redactor, TimeProvider clock, JsonSerializerOptions json)
    : IService<AdjudicateRequest, AdjudicateResponse>
{
    internal const int Samples = 3;

    // One adjudication run may take as long as a minute whatever the question's own budget.
    private static readonly TimeSpan _minimumBudget = TimeSpan.FromSeconds(60);

    public async ValueTask<Result<AdjudicateResponse>> Handle(AdjudicateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!QuestionCatalog.Names.Contains(request.Question))
        {
            return JudgeErrors.Invalid.With($"unknown question: {request.Question}");
        }

        var adjudicator = catalog.Adjudicator(request.Providers);
        if (adjudicator.IsError)
        {
            return Results.Failure<AdjudicateResponse>(adjudicator.Error);
        }

        var skipped = new HashSet<EvalItemId>();
        var (labeled, split, failed) = (0, 0, 0);
        for (var n = 0; n < request.Limit; n++)
        {
            var next = await store.NextUnlabeled(request.Question, LabelSource.Adjudicator, skipped, cancellationToken).ConfigureAwait(false);
            if (next.IsError)
            {
                return Results.Failure<AdjudicateResponse>(next.Error);
            }

            if (next.Value is not { } item)
            {
                break;
            }

            _ = skipped.Add(item.Id);
            if (Prepared(item) is not { } prepared)
            {
                failed++;
                continue;
            }

            switch (await Verdict(adjudicator.Value, prepared, cancellationToken).ConfigureAwait(false))
            {
                case { Label: { } label }:
                    var recorded = await store.RecordLabel(item.Id, LabelSource.Adjudicator, label, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                    if (recorded.IsError)
                    {
                        return Results.Failure<AdjudicateResponse>(recorded.Error);
                    }

                    labeled++;
                    break;
                case { AllErrored: true }:
                    failed++;
                    break;
                default:
                    split++;
                    break;
            }
        }

        return new AdjudicateResponse(labeled, split, failed);
    }

    // The item's input redacted (values and keys), then read as the question's input; null when it is not JSON, doesn't
    // fit the question, or is an image question (its child would need the Read tool, which the adjudicator never gets).
    private Ask? Prepared(EvalItem item)
    {
        if (!InputJson.TryParse(item.InputJson, out var parsed))
        {
            return null;
        }

        var safe = InputJson.Write(redactor.Redact(parsed), json);
        return QuestionCatalog.Prepare(item.Question, safe, json) is { Reference.ContentClass: not ContentClass.Image } prepared
            ? new Ask(AddOptions(prepared.Reference), prepared.Input, prepared.TimeBudget)
            : null;
    }

    private async ValueTask<Verdicts> Verdict(IModelProvider provider, Ask prepared, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = 0;
        var budget = prepared.TimeBudget > _minimumBudget ? prepared.TimeBudget : _minimumBudget;
        for (var sample = 0; sample < Samples; sample++)
        {
            var answer = await provider.Decide(prepared.Reference, prepared.Input, budget, cancellationToken).ConfigureAwait(false);
            if (answer is not ProviderDecided decided)
            {
                errors++;
            }
            else if (prepared.Reference.Outputs.Contains(decided.Decision, StringComparer.Ordinal))
            {
                counts[decided.Decision] = counts.GetValueOrDefault(decided.Decision) + 1;
            }
        }

        var top = counts.OrderByDescending(pair => pair.Value).FirstOrDefault();
        return (top.Value * 2) > Samples ? new Verdicts(top.Key, AllErrored: false) : new Verdicts(Label: null, errors == Samples);
    }

    // The options with what each means, so the stronger model judges by the same criteria the others are scored on.
    private static QuestionRef AddOptions(QuestionRef reference) =>
        reference.Criteria is not { Count: > 0 } criteria
            ? reference
            : reference with { Prompt = $"{reference.Prompt}\n\nOptions:\n{string.Join('\n', criteria.Select(pair => $"- {pair.Key}: {pair.Value}"))}" };

    private sealed record Ask(QuestionRef Reference, JsonNode? Input, TimeSpan TimeBudget);

    private sealed record Verdicts(string? Label, bool AllErrored);
}
