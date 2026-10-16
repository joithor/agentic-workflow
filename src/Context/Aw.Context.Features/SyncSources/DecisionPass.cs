using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Decision extraction and conflict checks for the records one sync page committed (spec §6.1, §8.8, §8.9; S-5B). The model sees only the scrubbed,
/// fenced record text and the node keys its rules linked, and its answer is validated by <see cref="DecisionExtractor"/>. Everything it asks is counted
/// against 'maxTokensPerSync': a call that would go over the cap is not made, and the record stays queued for the next sync.
/// </summary>
internal sealed class DecisionPass(IIntentStore store, IModelPort model, LabelingSettings labeling, (IIngestFilter Ingest, TimeProvider Clock) support)
{
    private const int ExtractAnswerTokens = 600;
    private const int VerdictAnswerTokens = 60;
    private static readonly TimeSpan _askBudget = TimeSpan.FromSeconds(60);

    private bool _stopped;

    public int Tokens { get; private set; }

    public int Refused { get; private set; }

    public bool HeldBack { get; private set; }

    public string? Failure { get; private set; }

    public async ValueTask Run(IReadOnlyList<PendingRecord> pending, CancellationToken cancellationToken)
    {
        foreach (var record in pending)
        {
            var linked = record.Links.Select(l => l.Target).Where(k => k.Kind is not (NodeKind.Record or NodeKind.Decision)).Distinct().ToList();
            if (linked.Count == 0)
            {
                _ = await store.SaveDecisions(new DecisionBatch(record.Record.Key, record.Record.Version, [], []), cancellationToken).ConfigureAwait(false);   // nothing it could decide: not queued
            }
            else if (!labeling.Enabled || _stopped)
            {
                HeldBack = true;
            }
            else
            {
                await Extract(record, linked, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask Extract(PendingRecord record, List<NodeKey> linked, CancellationToken cancellationToken)
    {
        var asked = await Ask(ModelPurpose.DecisionExtraction, DecisionExtractor.Prompt(record.Record.Key, record.Text, linked), ExtractAnswerTokens, cancellationToken).ConfigureAwait(false);
        if (asked is null)
        {
            return;
        }

        var read = DecisionExtractor.Read(asked, record.Record.Key, record.Text, linked, support.Clock.GetUtcNow());
        Refused += read.Refused;
        var decisions = read.Decisions.Select(d => d with { Wording = support.Ingest.Clean(d.Wording, singleLine: true) is { Blocked: false } clean ? clean.Text : d.Quote }).ToList();
        var saved = await store.SaveDecisions(new DecisionBatch(record.Record.Key, record.Record.Version, decisions, read.Edges), cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            Failure = saved.Error.Code;
            _stopped = true;
            return;
        }

        await CheckConflicts(decisions, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CheckConflicts(List<DecisionRow> added, CancellationToken cancellationToken)
    {
        var layer = await store.Layer(cancellationToken).ConfigureAwait(false);
        if (layer.IsError || added.Count == 0)
        {
            return;
        }

        foreach (var (first, second, _) in ConflictChecker.Pairs(layer.Value, added))
        {
            var answer = await Ask(ModelPurpose.Adjudication, ConflictChecker.Prompt(first, second), VerdictAnswerTokens, cancellationToken).ConfigureAwait(false);
            if (answer is null)
            {
                return;
            }

            if (ConflictChecker.Read(answer) is not { } verdict)
            {
                Refused++;
                continue;
            }

            _ = await store.SaveConflict(DecisionConflict.Of(first.ToKey(), second.ToKey(), verdict, support.Clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<string?> Ask(ModelPurpose purpose, string prompt, int answerTokens, CancellationToken cancellationToken)
    {
        if (_stopped || Tokens + ((prompt.Length + 3) / 4) + answerTokens > labeling.MaxTokensPerSync)
        {
            HeldBack = true;
            _stopped = true;
            return null;
        }

        var answer = await model.Ask(new ModelRequest(purpose, prompt, answerTokens, _askBudget), cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            Failure = answer.Error.Code;
            _stopped = true;
            return null;
        }

        Tokens += answer.Value.InputTokens + answer.Value.OutputTokens;
        return answer.Value.Text;
    }
}
