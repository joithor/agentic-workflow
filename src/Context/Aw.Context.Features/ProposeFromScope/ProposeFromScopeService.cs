using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ProposeFromScope;

/// <summary>
/// Writes a scoping run's new surfaces as 'propose' rows (spec §8.7): in the latest draft, which this opens as a copy of the
/// current map when there is none, so the current map is never written. A surface the map or an earlier call already holds, or
/// a person rejected, is no row; repeating a call is idempotent. The run's whole surface list is kept too (a second call replaces it), so its graph status can name every surface, mapped and unmapped ones included. A repo the caller can't address is SND-CONTEXT-045, the same
/// answer whether the profile lacks it or the session's scope excludes it.
/// </summary>
internal sealed class ProposeFromScopeService(IGraphStore store, IProposalRuns runs, TimeProvider clock) : IService<ProposeFromScopeRequest, ProposeResult>
{
    internal const string Who = "service:engine";

    public async ValueTask<Result<ProposeResult>> Handle(ProposeFromScopeRequest request, CancellationToken cancellationToken)
    {
        var proposal = request.Proposal;
        if (Addressable(request.Scope, proposal) is { } refused)
        {
            return refused;
        }

        var now = clock.GetUtcNow();
        var opened = await store.OpenDraft(now, cancellationToken).ConfigureAwait(false);
        var draft = opened.IsError ? opened.Error : await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (opened.IsError || draft.IsError)
        {
            return opened.IsError ? opened.Error : draft.Error;
        }

        var plan = ProposalPlanner.Plan(proposal, draft.Value.Graph, draft.Value.Overrides, Who, now);
        var written = await Write(plan, proposal, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new ProposeResult(opened.Value, plan.Rows.Count, plan.Existing, plan.Rejected);
    }

    // The rows, then the run's whole surface list.
    private async ValueTask<Result<int>> Write(ProposalPlan plan, ScopeProposal proposal, CancellationToken cancellationToken)
    {
        var added = await store.AppendOverrides(plan.Rows, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added : await runs.SaveRun(proposal, cancellationToken).ConfigureAwait(false);
    }

    private static Error? Addressable(ContextScope scope, ScopeProposal proposal)
    {
        var addressed = SettingsReader.Read(scope.Settings).Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        if (addressed.IsError && addressed.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return addressed.Error;
        }

        return addressed.IsError || !addressed.Value.Any(r => r.Name == proposal.Repo)
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can address (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : null;
    }
}
