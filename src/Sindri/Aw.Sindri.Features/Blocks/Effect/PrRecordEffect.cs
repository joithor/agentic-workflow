using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.record' (PB spec §4.7): appends a 'recheck' or 'docs' row to the merge record, 'babysit.jsonl' in the instance's run area, written from the fact
/// the child returned. The shared 'MergeRecord' writes it, keyed by the effect identity so a repeated dispatch writes once; the row carries the head 'fact:prState' holds and the result. It replies nothing.
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
        if (!context.Arguments.TryGetValue("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() is not ("recheck" or "docs"))
        {
            return PrEffectFacts.Missing(context, "kind 'recheck' or 'docs'");
        }

        var result = await PrEffectFacts.Content(context, "result", contents, cancellationToken).ConfigureAwait(false);
        var state = result.IsError ? result.Error : await PrEffectFacts.Held(context, "fact:prState", contents, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        var fields = new JsonObject { ["result"] = JsonNode.Parse(result.Value.GetRawText()) };
        var row = new MergeRow(context.Instance, context.Identity, kind.GetString()!, state.Value is { } held ? PrEffectFacts.Head(held) : null, fields);   // why: checked to be a string just above
        var written = await record.Append(row, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: !written.Value);
    }
}
