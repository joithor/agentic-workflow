using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.record' (PB spec §4.7): appends a 'recheck', 'docs', 'docsMerge' or 'end' row to the merge record, 'babysit.jsonl' in the instance's run area. 'recheck' and 'docs' are written from the fact
/// the child returned; 'docsMerge' is the owner desk's 'doneByHand' or 'left' (outcome, trigger, and the docs PR 'fact:docsMerge' holds); 'end' names the terminal the transition enters and, on 'rework', the hash of the rework notes. The shared 'MergeRecord' writes it, keyed by the effect identity so a repeated dispatch writes once; the row carries the head 'fact:prState' holds and the result. It replies nothing.
/// </summary>
/// <param name="record">The merge record's writer.</param>
/// <param name="contents">Reads the facts it takes.</param>
internal sealed class PrRecordEffect(IMergeRecord record, IFactContents contents) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Record;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Arguments.TryGetValue("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() is not ("recheck" or "docs" or "end" or "docsMerge"))
        {
            return PrEffectFacts.Missing(context, "kind 'recheck', 'docs', 'end' or 'docsMerge'");
        }

        var name = kind.GetString()!;   // why: checked to be a string just above
        var fields = name switch
        {
            "end" => await EndFields(context, cancellationToken).ConfigureAwait(false),
            "docsMerge" => await DocsMergeFields(context, cancellationToken).ConfigureAwait(false),
            _ => await ResultFields(context, cancellationToken).ConfigureAwait(false),
        };
        var state = fields.IsError ? fields.Error : await PrEffectFacts.Held(context, "fact:prState", contents, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        // A docsMerge row's headSha is the docs PR's head, which the desk does not hold.
        var head = name is not "docsMerge" && state.Value is { } held ? PrEffectFacts.Head(held) : null;
        var row = new MergeRow(context.Instance, context.Identity, name, head, fields.Value);
        var written = await record.Append(row, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: !written.Value);
    }

    private async ValueTask<Result<JsonObject>> ResultFields(EffectContext context, CancellationToken cancellationToken)
    {
        var result = await PrEffectFacts.Content(context, "result", contents, cancellationToken).ConfigureAwait(false);
        return result.IsError ? result.Error : new JsonObject { ["result"] = JsonNode.Parse(result.Value.GetRawText()) };
    }

    // The owner desk's docsMerge row (done by hand, left open): the docs PR 'fact:docsMerge' holds, if any, and the trigger the recipe names.
    private async ValueTask<Result<JsonObject>> DocsMergeFields(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("outcome", out var outcome) || outcome.ValueKind != JsonValueKind.String || outcome.GetString() is not ("doneByHand" or "left"))
        {
            return PrEffectFacts.Missing(context, "outcome 'doneByHand' or 'left'");
        }

        var held = await PrEffectFacts.Held(context, "fact:docsMerge", contents, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        return new JsonObject
        {
            ["docsPr"] = held.Value is { } docs ? PrReplyFacts.Text(docs, "docsPr") : null,
            ["outcome"] = outcome.GetString(),
            ["trigger"] = context.Arguments.TryGetValue("trigger", out var trigger) && trigger.ValueKind == JsonValueKind.String ? trigger.GetString() : null,
            ["actor"] = null,
        };
    }

    // The end row names the terminal; on 'rework' it also carries the hash of the owner's notes, never the notes (spec 4.7).
    private async ValueTask<Result<JsonObject>> EndFields(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("terminal", out var terminal) || terminal.ValueKind != JsonValueKind.String || terminal.GetString() is not ("merged" or "closed" or "stopped" or "rework"))
        {
            return PrEffectFacts.Missing(context, "terminal 'merged', 'closed', 'stopped' or 'rework'");
        }

        var fields = new JsonObject { ["terminal"] = terminal.GetString() };
        if (terminal.GetString() != "rework")
        {
            return fields;
        }

        var notes = await PrEffectFacts.Content(context, "notes", contents, cancellationToken).ConfigureAwait(false);
        if (notes.IsError)
        {
            return notes.Error;
        }

        fields["reworkNotesHash"] = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(notes.Value.GetRawText())));
        return fields;
    }
}
