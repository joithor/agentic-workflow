using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.restack' (PB spec §4.6): moves a stacked pull request onto its new base. The new base is the pull request's own base branch when the pull request that owns it is still open (the base moved), else the
/// repository's default branch (the base merged). The workspace ('workspace', else 'fact:workspace') must be at the pull request's head; the new base is fetched and the branch rebased onto it, dropping the commits the old
/// base brought: with 'from' (the old base's tip: the argument, else 'fact:prState' base.sha) only the commits after it are replayed, which survives a squash merge whose patches match but whose shas don't; without it git drops
/// the commits the new base already has by patch. The rebased range is scanned for secrets in process (SHIP-SECRET-001), the pull request is moved to the new base and the head pushed with a lease on the head the forge saw.
/// It replies 'pr.restacked' with 'restacked', 'nothing' (the pull request is closed, or already on its new base), 'conflict' (git could not merge; the paths are in the fact) or 'refused' (a scan hit by file and line, a workspace
/// that isn't at the head, or a push the lease or the remote turned away); a conflict or a refusal pushes and retargets nothing and leaves the workspace on the head it had. It carries 'fact:restack' ({ outcome, oldBase, newBase, head,
/// reason, paths }) and appends a 'restack' row (old base, new base, outcome) for every outcome, once per effect identity.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="diff">Reads the workspace's head.</param>
/// <param name="rebase">The workspace's git: fetch, rebase, reset.</param>
/// <param name="scan">The in-process secret scan of the rebased range.</param>
/// <param name="facts">Reads 'pr', 'fact:workspace' and 'fact:prState' back.</param>
/// <param name="store">Stores 'fact:restack'.</param>
/// <param name="record">The merge record, where the restack row goes.</param>
internal sealed class PrRestackEffect(IPullRequests pulls, IRunDiff diff, IWorkspaceRebase rebase, RebasedRangeScan scan, IFactContents facts, IFactStore store, IMergeRecord record) : IEffectKind, IDescribedBlock
{
    private static readonly OutcomeName _restacked = OutcomeName.From("restacked");
    private static readonly OutcomeName _nothing = OutcomeName.From("nothing");
    private static readonly OutcomeName _conflict = OutcomeName.From("conflict");
    private static readonly OutcomeName _refused = OutcomeName.From("refused");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Restack;

    private sealed record Target(string Link, PullRepo Repo, string Workspace, string? From);

    private sealed record Ended(OutcomeName Outcome, string OldBase, string NewBase, string? Head, string? Reason, IReadOnlyList<string> Paths);

    /// <summary>The workspace at the pull request's head, the new base fetched, and what was seen of the pull request.</summary>
    private sealed record Ready(PullView View, string Onto, string OntoSha);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var target = await Read(context, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        var ended = await Restack(target.Value, cancellationToken).ConfigureAwait(false);
        return ended.IsError ? ended.Error : await Done(context, ended.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Ended>> Restack(Target target, CancellationToken cancellationToken)
    {
        var view = await pulls.Get(target.Link, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (view.Value.State != PullState.Open)
        {
            return new Ended(_nothing, view.Value.Base, view.Value.Base, view.Value.HeadSha, "the pull request is not open", []);
        }

        var onto = await NewBase(target.Repo, view.Value.Base, cancellationToken).ConfigureAwait(false);
        if (onto.IsError)
        {
            return onto.Error;
        }

        var head = await diff.Head(target.Workspace, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (!string.Equals(head.Value, view.Value.HeadSha, StringComparison.Ordinal))
        {
            return Refused(view.Value, onto.Value, $"the workspace is at {head.Value}, not at the pull request's head {view.Value.HeadSha}; nothing is rebased");
        }

        var sha = await rebase.Fetch(target.Workspace, onto.Value, cancellationToken).ConfigureAwait(false);
        return sha.IsError ? sha.Error : await Move(target, new Ready(view.Value, onto.Value, sha.Value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A head that has the new base already is only retargeted (or is nothing at all); otherwise it is rebased.</summary>
    private async ValueTask<Result<Ended>> Move(Target target, Ready ready, CancellationToken cancellationToken)
    {
        var view = ready.View;
        var onBase = string.Equals(view.Base, ready.Onto, StringComparison.Ordinal);
        var has = await rebase.Contains(target.Workspace, ready.OntoSha, view.HeadSha, cancellationToken).ConfigureAwait(false);
        if (has.IsError)
        {
            return has.Error;
        }

        if (!has.Value)
        {
            return await Rebase(target, ready, cancellationToken).ConfigureAwait(false);
        }

        if (onBase)
        {
            return new Ended(_nothing, view.Base, ready.Onto, view.HeadSha, "the pull request is on its base and has it", []);
        }

        var retargeted = await pulls.Retarget(target.Link, ready.Onto, cancellationToken).ConfigureAwait(false);
        return retargeted.IsError ? retargeted.Error : new Ended(_restacked, view.Base, ready.Onto, view.HeadSha, Reason: null, []);
    }

    private async ValueTask<Result<Ended>> Rebase(Target target, Ready ready, CancellationToken cancellationToken)
    {
        var view = ready.View;
        var rebased = await rebase.Rebase(target.Workspace, ready.OntoSha, target.From, cancellationToken).ConfigureAwait(false);
        if (rebased.IsError)
        {
            return rebased.Error;
        }

        if (rebased.Value.Head is not { } head)
        {
            return new Ended(_conflict, view.Base, ready.Onto, view.HeadSha, "git could not merge the pull request onto its new base", rebased.Value.Conflicts);
        }

        var landed = await Land(target, ready, head, cancellationToken).ConfigureAwait(false);
        if (landed.IsError || landed.Value.Outcome != _restacked)
        {
            _ = await rebase.Reset(target.Workspace, view.HeadSha, cancellationToken).ConfigureAwait(false);   // why: a refusal or a failure leaves the workspace on the head it had
        }

        return landed;
    }

    /// <summary>Scans the rebased range, moves the pull request to its new base and pushes the new head under a lease on the head the forge saw.</summary>
    private async ValueTask<Result<Ended>> Land(Target target, Ready ready, string head, CancellationToken cancellationToken)
    {
        var view = ready.View;
        var hits = await scan.Hits(target.Workspace, ready.OntoSha, cancellationToken).ConfigureAwait(false);
        if (hits.IsError)
        {
            return hits.Error;
        }

        if (hits.Value.Count > 0)
        {
            return new Ended(_refused, view.Base, ready.Onto, view.HeadSha, "SHIP-SECRET-001: a rebased line holds a secret; remove it and amend, nothing is pushed", hits.Value);
        }

        var moved = string.Equals(view.Base, ready.Onto, StringComparison.Ordinal) ? Results.Success(Unit.Value) : await pulls.Retarget(target.Link, ready.Onto, cancellationToken).ConfigureAwait(false);
        if (moved.IsError)
        {
            return moved.Error;
        }

        var pushed = await pulls.Push(new PullPush(target.Link, target.Workspace, head, view.HeadSha), cancellationToken).ConfigureAwait(false);
        if (pushed.IsError)
        {
            return pushed.Error;
        }

        return pushed.Value == PullPushOutcome.Pushed
            ? new Ended(_restacked, view.Base, ready.Onto, head, Reason: null, [])
            : new Ended(_refused, view.Base, ready.Onto, view.HeadSha, "the forge turned the push away: the pull request's head moved or the remote refused it", []);
    }

    // The branch that owns the pull request's base: the base itself while its pull request is open (it moved), else the default branch (it merged, and the forge may have moved the base there already).
    private async ValueTask<Result<string>> NewBase(PullRepo repo, string current, CancellationToken cancellationToken)
    {
        var fallback = await pulls.DefaultBranch(repo, cancellationToken).ConfigureAwait(false);
        if (fallback.IsError || string.Equals(current, fallback.Value, StringComparison.Ordinal))
        {
            return fallback;
        }

        var parent = await pulls.Find(new FindPull(repo, current, Marker: null), cancellationToken).ConfigureAwait(false);
        return parent.IsError ? parent.Error : parent.Value is null ? fallback.Value : current;
    }

    private static Ended Refused(PullView view, string onto, string reason) => new(_refused, view.Base, onto, view.HeadSha, reason, []);

    /// <summary>The reply: the outcome, 'fact:restack' and the record row.</summary>
    private async ValueTask<Result<EffectDone>> Done(EffectContext context, Ended ended, CancellationToken cancellationToken)
    {
        var paths = new JsonArray();
        foreach (var path in ended.Paths)
        {
            paths.Add((JsonNode)JsonValue.Create(path));
        }

        var fact = new JsonObject { ["outcome"] = ended.Outcome.Value, ["oldBase"] = ended.OldBase, ["newBase"] = ended.NewBase, ["head"] = ended.Head, ["reason"] = ended.Reason, ["paths"] = paths };
        var stored = await store.Put(Encoding.UTF8.GetBytes(fact.ToJsonString()), cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var row = new JsonObject { ["oldBase"] = ended.OldBase, ["newBase"] = ended.NewBase, ["outcome"] = ended.Outcome.Value };
        var recorded = await record.Append(new MergeRow(context.Instance, context.Identity, "restack", ended.Head, row), cancellationToken).ConfigureAwait(false);
        return recorded.IsError
            ? recorded.Error
            : new EffectDone(ended.Outcome, Existed: false, new Dictionary<FactKey, ReplyFact> { [FactKey.From("fact:restack")] = new(stored.Value.Ref, stored.Value.Sha256, ended.Head) });
    }

    private async ValueTask<Result<Target>> Read(EffectContext context, CancellationToken cancellationToken)
    {
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        var named = await PrReplyFacts.Argument(facts, context, "workspace", cancellationToken).ConfigureAwait(false);
        var held = await PrReplyFacts.Fact(facts, context, "fact:workspace", cancellationToken).ConfigureAwait(false);
        var state = await PrReplyFacts.Fact(facts, context, "fact:prState", cancellationToken).ConfigureAwait(false);
        var from = await PrReplyFacts.Argument(facts, context, "from", cancellationToken).ConfigureAwait(false);
        if (PrReplyFacts.FirstError(pr, named, held, state, from) is { } unreadable)
        {
            return unreadable;
        }

        var workspace = PrReplyFacts.Text(named.Value, "path") ?? PrReplyFacts.Text(held.Value, "path");
        if (DocsPulls.Source(PrReplyFacts.Text(pr.Value, "link", "url", "pr")) is not { } source || string.IsNullOrWhiteSpace(workspace))
        {
            return PrEffectFacts.Missing(context, "the pull request's link ('pr') and the workspace ('workspace' or 'fact:workspace')");
        }

        return new Target(PrReplyFacts.Text(pr.Value, "link", "url", "pr")!, source.Repo, workspace.Trim(), PrReplyFacts.Text(from.Value) ?? DocsPulls.Nested(state.Value, "base", "sha"));   // why: Source(link) was non-null, so the link is
    }
}
