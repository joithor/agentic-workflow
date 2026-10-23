using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.merge-docs' (PB spec §4.6, BD9): merges the stacked docs pull request of the source pull request 'pr' names, after the source merged. The docs pull request is found by
/// its branch and its marker (the same ones 'effect.pr.create-stacked' gives it). One that is not there, or is closed or merged already, replies 'none'. One whose required
/// checks fail or still run, or that has a conflict, stays open and replies 'pending', as does a merge the forge blocks or whose head moved; nothing waits, and the owner's desk
/// (or the next dispatch) retries. Otherwise its base is moved to the default branch if it is not there and it is merged with the forge's expected-head check, by the method of
/// 'effect.pr.merge': 'merged'. It replies 'pr.docsMerged' with 'fact:docsMerge' ({ docsPr, outcome, sha }) and appends a 'docsMerge' row for every outcome (docs PR, outcome, and the
/// optional 'trigger' and 'actor' arguments). The human-trigger rule (W23) is the engine's, not this effect's.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="store">Stores 'fact:docsMerge'.</param>
/// <param name="profile">The approved profile: the merge method when the recipe names none.</param>
/// <param name="record">The merge record, where the docsMerge row goes.</param>
internal sealed class PrMergeDocsEffect(IPullRequests pulls, IFactContents facts, IFactStore store, IApprovedProfile profile, IMergeRecord record) : IEffectKind, IDescribedBlock
{
    private static readonly OutcomeName _merged = OutcomeName.From("merged");
    private static readonly OutcomeName _pending = OutcomeName.From("pending");
    private static readonly OutcomeName _none = OutcomeName.From("none");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.MergeDocs;

    private sealed record Ended(OutcomeName Outcome, string? DocsPr, string? HeadSha, string? MergedSha);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        if (pr.IsError)
        {
            return pr.Error;
        }

        var sourceHead = DocsPulls.Nested(pr.Value, "head", "ref");
        if (DocsPulls.Source(PrReplyFacts.Text(pr.Value, "link", "url", "pr")) is not { } source || string.IsNullOrEmpty(sourceHead))
        {
            return Missing(context);
        }

        var ended = await Merge(context, source, DocsPulls.Branch(sourceHead), cancellationToken).ConfigureAwait(false);
        return ended.IsError ? ended.Error : await Done(context, ended.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Ended>> Merge(EffectContext context, (PullRepo Repo, int Number) source, string branch, CancellationToken cancellationToken)
    {
        var found = await pulls.Find(new FindPull(source.Repo, branch, DocsPulls.Marker(source)), cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } docs)
        {
            return new Ended(_none, DocsPr: null, HeadSha: null, MergedSha: null);
        }

        var view = await pulls.Get(docs.Link, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (view.Value.State != PullState.Open)
        {
            return new Ended(_none, docs.Link, view.Value.HeadSha, MergedSha: null);
        }

        var held = new Ended(_pending, docs.Link, view.Value.HeadSha, MergedSha: null);
        return view.Value.Checks.State is PullChecksState.Failing or PullChecksState.Pending || view.Value.Mergeable == false
            ? held
            : await Land(context, source.Repo, view.Value, held, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves the docs pull request to the default branch and merges it over the head just read; a merge the forge does not make leaves it pending.</summary>
    private async ValueTask<Result<Ended>> Land(EffectContext context, PullRepo repo, PullView docs, Ended held, CancellationToken cancellationToken)
    {
        var method = await PrMergeMethods.Resolve(context, profile, cancellationToken).ConfigureAwait(false);
        if (method.IsError)
        {
            return method.Error;
        }

        var retargeted = await Retarget(repo, docs.Link, docs.Base, cancellationToken).ConfigureAwait(false);
        if (retargeted.IsError)
        {
            return retargeted.Error;
        }

        var merged = await pulls.Merge(new PullMerge(docs.Link, method.Value, docs.HeadSha), cancellationToken).ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        return merged.Value.Status == PullMergeStatus.Merged ? held with { Outcome = _merged, MergedSha = merged.Value.MergedSha } : held;
    }

    // why: the source merged, so a docs pull request still based on its head branch must go to the default branch before it can merge.
    private async ValueTask<Result<Unit>> Retarget(PullRepo repo, string link, string current, CancellationToken cancellationToken)
    {
        var target = await pulls.DefaultBranch(repo, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        return string.Equals(current, target.Value, StringComparison.Ordinal) ? Unit.Value : await pulls.Retarget(link, target.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The reply: the outcome, 'fact:docsMerge' and the record row.</summary>
    private async ValueTask<Result<EffectDone>> Done(EffectContext context, Ended ended, CancellationToken cancellationToken)
    {
        var fact = new JsonObject { ["docsPr"] = ended.DocsPr, ["outcome"] = ended.Outcome.Value, ["sha"] = ended.MergedSha };
        var stored = await store.Put(Encoding.UTF8.GetBytes(fact.ToJsonString()), cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var row = new JsonObject
        {
            ["docsPr"] = ended.DocsPr,
            ["outcome"] = ended.Outcome.Value,
            ["trigger"] = Argument(context, "trigger"),
            ["actor"] = Argument(context, "actor"),
        };
        var recorded = await record.Append(new MergeRow(context.Instance, context.Identity, "docsMerge", ended.HeadSha, row), cancellationToken).ConfigureAwait(false);
        return recorded.IsError
            ? recorded.Error
            : new EffectDone(ended.Outcome, Existed: false, new Dictionary<FactKey, ReplyFact> { [FactKey.From("fact:docsMerge")] = new(stored.Value.Ref, stored.Value.Sha256, ended.HeadSha) });
    }

    private static string? Argument(EffectContext context, string name) => context.Arguments.TryGetValue(name, out var value) ? PrReplyFacts.Text(value) : null;

    private static Error Missing(EffectContext context) =>
        PrEffectFacts.Missing(context, "the source pull request's link and head branch ('pr': { link, head: { ref } })");
}
