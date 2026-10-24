using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.relevance-judge' on the block runtime (spec 4.2): one judge call per entry of 'fact:lenses', 'parallel' at a time, over <see cref="RelevanceJudgeProjection"/> only, so the judge never sees the agent's labels.
/// An escalated answer, a choice that is not one of the three, or one failed call is class 'uncertain' with its reason code; the Step fails only when every call failed, or when the very first call says the judge did not finish
/// (it is not available, so asking the rest would only repeat that). The first call runs alone for that reason. The providers are the runner's resolved list.
/// </summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the projection's text before it leaves for the judge.</param>
internal sealed class RelevanceJudgeStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    internal const double DefaultThreshold = 0.6;

    internal const int DefaultParallel = 4;

    internal const int MaxParallel = 16;

    private const string Name = "step.scope.relevance-judge";
    private const string Uncertain = "uncertain";
    private const int ReasonCap = 200;

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:brief", "fact:lenses") is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"{Name} needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var threshold = context.Arguments.TryGetValue("threshold", out var given) && given.ValueKind == JsonValueKind.Number ? given.GetDouble() : DefaultThreshold;
        if (threshold is <= 0 or > 1)
        {
            return BlockStepResult.Failed($"{Name}: the threshold {threshold.ToString(CultureInfo.InvariantCulture)} must be above 0 and at most 1");
        }

        IReadOnlyList<string> providers = context.Arguments.TryGetValue("providers", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty)]
            : JudgeProviders.Default;
        var parallel = context.Arguments.TryGetValue("parallel", out var width) && width.ValueKind == JsonValueKind.Number && width.TryGetInt32(out var asked) ? Math.Clamp(asked, 1, MaxParallel) : DefaultParallel;

        var lenses = StepFacts.Read(context, "fact:lenses");
        var briefText = ScopeJson.Str(StepFacts.Read(context, "fact:brief"), "text") ?? string.Empty;
        var entries = RelevanceEntries.Of(lenses);
        var requests = entries.Select(e => RelevanceJudgeProjection.Request(e, briefText, [.. ScopeJson.Strings(ScopeJson.Obj(lenses, e.Lens), "anchors")], threshold, providers, scrubber)).ToList();
        var answers = new ChooseAnswer[entries.Count];
        if (entries.Count > 0)
        {
            answers[0] = await judge.Choose(requests[0], cancellationToken).ConfigureAwait(false);
            if (answers[0].Status == ChooseStatus.Failed && answers[0].Reason.Contains("did not finish", StringComparison.OrdinalIgnoreCase))
            {
                return BlockStepResult.Failed($"{Name}: {answers[0].Reason}");
            }

            await Parallel.ForEachAsync(
                Enumerable.Range(1, entries.Count - 1),
                new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = cancellationToken },
                async (index, token) => answers[index] = await judge.Choose(requests[index], token).ConfigureAwait(false)).ConfigureAwait(false);

            if (answers.All(static a => a.Status == ChooseStatus.Failed))
            {
                return BlockStepResult.Failed($"{Name}: every judge call failed ({answers[0].Reason})");
            }
        }

        var rulings = new JsonArray();
        for (var i = 0; i < entries.Count; i++)
        {
            ScopeJson.Append(rulings, Ruling(entries[i], answers[i]));
        }

        return StepFacts.Done("ruled", ("fact:relevanceRulings", new JsonObject
        {
            ["judge"] = new JsonObject { ["providers"] = ScopeJson.ToArray(providers), ["threshold"] = threshold },
            ["rulings"] = rulings,
        }));
    }

    private JsonObject Ruling(RelevanceEntry entry, ChooseAnswer answer)
    {
        var decided = answer.Status == ChooseStatus.Decided;
        if (decided && answer.Choice is { } choice && RelevanceJudgeProjection.Choices.Any(c => string.Equals(c.Name, choice, StringComparison.Ordinal)))
        {
            return Row(entry, choice, answer.Confidence, answer.Provider, answer.DecisionRef, Reason(answer.Reason));
        }

        var code = decided ? "invalid-choice" : Reason(answer.Reason);
        return Row(entry, Uncertain, confidence: null, provider: null, answer.DecisionRef, code);
    }

    private string Reason(string reason)
    {
        var clean = scrubber.Scrub(reason).Trim();
        return clean.Length <= ReasonCap ? clean : clean[..ReasonCap];
    }

    private static JsonObject Row(RelevanceEntry entry, string cls, double? confidence, string? provider, string? decisionId, string reasonCode) => new()
    {
        ["id"] = entry.Id,
        ["class"] = cls,
        ["confidence"] = confidence,
        ["provider"] = provider,
        ["decisionId"] = decisionId,
        ["reasonCode"] = reasonCode,
    };
}
