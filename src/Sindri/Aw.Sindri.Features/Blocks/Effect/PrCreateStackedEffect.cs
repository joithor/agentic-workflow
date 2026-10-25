using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.create-stacked' (PB spec §4.6, §7.2): pushes the docs workspace's head to 'docs/&lt;source head branch&gt;' with a lease and opens a pull request whose base is the source
/// pull request's head branch, or updates the one Sindri already has open there. The docs pull request is found by its branch and a marker derived from the source, never by
/// the branch alone. The branch is created when absent; otherwise it is pushed only over the sha Sindri last pushed ('fact:docsPr'), or, with no such fact, over the open docs
/// pull request's head. It replies 'pr.stacked' with 'opened' or 'updated' and 'fact:docsPr' ({ link, number, state, base, head, source, syncedHead }); a branch someone else
/// moved, a push the remote rejects and a call the forge refuses reply 'rejected', and any other forge failure fails the effect. The arguments are 'pr' (the source: its link and
/// 'head.ref'), 'on' (the snapshot whose head the docs describe), 'workspace' and 'title'.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="repos">Which repository the docs workspace belongs to; it must be the source's.</param>
/// <param name="diff">Reads the docs workspace's head.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="store">Stores 'fact:docsPr'.</param>
internal sealed class PrCreateStackedEffect(IPullRequests pulls, IForgeRepos repos, IRunDiff diff, IFactContents contents, IFactStore store) : IEffectKind, IDescribedBlock
{
    private static readonly OutcomeName _opened = OutcomeName.From("opened");
    private static readonly OutcomeName _updated = OutcomeName.From("updated");
    private static readonly OutcomeName _rejected = OutcomeName.From("rejected");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.CreateStacked;

    private sealed record Plan(string Link, PullRepo Repo, string Marker, string SourceHead, string SyncedHead, string Workspace, string DocsHead, string Title);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await Read(context, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var held = await PrEffectFacts.Held(context, "fact:docsPr", contents, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        var done = await Push(plan.Value, held.Value is { } fact ? PrEffectFacts.Head(fact) : null, cancellationToken).ConfigureAwait(false);
        return done.IsError && string.Equals(done.Error.Code, SindriErrors.ForgeRefused.Code, StringComparison.Ordinal)
            ? new EffectDone(_rejected, Existed: false)
            : done;
    }

    /// <summary>Finds the docs pull request first, then pushes the branch under the lease and opens or updates the pull request.</summary>
    private async ValueTask<Result<EffectDone>> Push(Plan plan, string? pushedBefore, CancellationToken cancellationToken)
    {
        var branch = DocsPulls.Branch(plan.SourceHead);
        var found = await pulls.Find(new FindPull(plan.Repo, branch, plan.Marker), cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        string? lease = null;
        string? currentHead = null;
        if (found.Value is { } existing)
        {
            var view = await pulls.Get(existing.Link, cancellationToken).ConfigureAwait(false);
            if (view.IsError)
            {
                return view.Error;
            }

            currentHead = view.Value.HeadSha;
            lease = pushedBefore ?? currentHead;
        }

        var pushed = await pulls.PushBranch(new PullBranchPush(plan.Repo, plan.Workspace, branch, plan.DocsHead, lease), cancellationToken).ConfigureAwait(false);
        if (pushed.IsError)
        {
            return pushed.Error;
        }

        // why: a dispatch repeated after its push landed finds the branch at its own commit, which the lease reads as a move
        var landed = pushed.Value == PullPushOutcome.HeadMoved && string.Equals(currentHead, plan.DocsHead, StringComparison.Ordinal);
        return pushed.Value == PullPushOutcome.Pushed || landed
            ? await OpenOrUpdate(plan, branch, found.Value, cancellationToken).ConfigureAwait(false)
            : new EffectDone(_rejected, Existed: false);
    }

    private async ValueTask<Result<EffectDone>> OpenOrUpdate(Plan plan, string branch, OpenedPull? existing, CancellationToken cancellationToken)
    {
        var body = $"Docs sync for {plan.Link}, written against its head {plan.SyncedHead}.";
        if (existing is not null)
        {
            // why: an update replaces the body whole, marker included, and the forge port appends the marker only on open.
            var updated = await pulls.Update(existing.Link, plan.Title, body + "\n\n<!-- aw-marker:" + plan.Marker + " -->", cancellationToken).ConfigureAwait(false);
            return updated.IsError ? updated.Error : await Done(_updated, plan, branch, existing, cancellationToken).ConfigureAwait(false);
        }

        var opened = await pulls.Open(new OpenPull(plan.Repo, branch, plan.SourceHead, plan.DocsHead, plan.Title, body, Draft: false, plan.Marker), cancellationToken).ConfigureAwait(false);
        return opened.IsError ? opened.Error : await Done(_opened, plan, branch, opened.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<EffectDone>> Done(OutcomeName outcome, Plan plan, string branch, OpenedPull pull, CancellationToken cancellationToken)
    {
        var fact = new JsonObject
        {
            ["link"] = pull.Link,
            ["number"] = pull.Number,
            ["state"] = "open",
            ["base"] = new JsonObject { ["ref"] = plan.SourceHead },
            ["head"] = new JsonObject { ["ref"] = branch, ["sha"] = plan.DocsHead },
            ["source"] = plan.Link,
            ["syncedHead"] = plan.SyncedHead,
        };
        var stored = await store.Put(Encoding.UTF8.GetBytes(fact.ToJsonString()), cancellationToken).ConfigureAwait(false);
        return stored.IsError
            ? stored.Error
            : new EffectDone(outcome, Existed: false, new Dictionary<FactKey, ReplyFact> { [FactKey.From("fact:docsPr")] = new(stored.Value.Ref, stored.Value.Sha256, plan.DocsHead) });
    }

    private async ValueTask<Result<Plan>> Read(EffectContext context, CancellationToken cancellationToken)
    {
        var pr = await PrReplyFacts.Argument(contents, context, "pr", cancellationToken).ConfigureAwait(false);
        var on = await PrReplyFacts.Argument(contents, context, "on", cancellationToken).ConfigureAwait(false);
        var workspace = await PrReplyFacts.Argument(contents, context, "workspace", cancellationToken).ConfigureAwait(false);
        if (PrReplyFacts.FirstError(pr, on, workspace) is { } unreadable)
        {
            return unreadable;
        }

        var link = PrEffectFacts.Link(pr.Value);
        var path = PrEffectFacts.Path(workspace.Value);
        var sourceHead = DocsPulls.Nested(pr.Value, "head", "ref") ?? DocsPulls.Nested(on.Value, "head", "ref");
        var syncedHead = PrEffectFacts.Head(on.Value) ?? DocsPulls.Nested(pr.Value, "head", "sha");
        if (DocsPulls.Source(link) is not { } source || string.IsNullOrEmpty(sourceHead) || string.IsNullOrEmpty(syncedHead) || string.IsNullOrEmpty(path))
        {
            return PrEffectFacts.Missing(context, "the source pull request's link and head branch ('pr'), the head it was read at ('on') and the docs workspace ('workspace')");
        }

        var title = context.Arguments.TryGetValue("title", out var named) ? PrReplyFacts.Text(named) : null;
        return await Workspace(context, source, new Plan(link!, source.Repo, DocsPulls.Marker(source), sourceHead, syncedHead, path, DocsHead: string.Empty, string.IsNullOrWhiteSpace(title) ? "docs: sync" : title), cancellationToken).ConfigureAwait(false);   // why: Source(link) was non-null, so the link is
    }

    /// <summary>Checks the docs workspace belongs to the source's repository and reads its head, the commit to push.</summary>
    private async ValueTask<Result<Plan>> Workspace(EffectContext context, (PullRepo Repo, int Number) source, Plan plan, CancellationToken cancellationToken)
    {
        var repo = await repos.OfWorkspace(plan.Workspace, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        if (repo.Value != source.Repo)
        {
            return PrEffectFacts.Missing(context, $"a docs workspace of {source.Repo.Owner}/{source.Repo.Name} (its origin is {repo.Value.Owner}/{repo.Value.Name})");
        }

        var head = await diff.Head(plan.Workspace, cancellationToken).ConfigureAwait(false);
        return head.IsError ? head.Error : plan with { DocsHead = head.Value };
    }
}
