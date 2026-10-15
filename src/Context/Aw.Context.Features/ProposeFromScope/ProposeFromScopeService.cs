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
/// a person rejected, is no row; repeating a call is idempotent. A repo the caller can't address is SND-CONTEXT-045, the same
/// answer whether the profile lacks it or the session's scope excludes it.
/// </summary>
internal sealed class ProposeFromScopeService(IGraphStore store, TimeProvider clock) : IService<ProposeFromScopeRequest, ProposeResult>
{
    internal const string Who = "service:engine";

    public async ValueTask<Result<ProposeResult>> Handle(ProposeFromScopeRequest request, CancellationToken cancellationToken)
    {
        var proposal = request.Proposal;
        var addressed = SettingsReader.Read(request.Scope.Settings).Bind(s => ScopeRules.Resolve(s, request.Scope.Repos, request.Scope.Caller));
        if (addressed.IsError && addressed.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return addressed.Error;
        }

        if (addressed.IsError || !addressed.Value.Any(r => r.Name == proposal.Repo))
        {
            return ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can address (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)");
        }

        var now = clock.GetUtcNow();
        var opened = await store.OpenDraft(now, cancellationToken).ConfigureAwait(false);
        var draft = opened.IsError ? opened.Error : await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (opened.IsError || draft.IsError)
        {
            return opened.IsError ? opened.Error : draft.Error;
        }

        var plan = ProposalPlanner.Plan(proposal, draft.Value.Graph, draft.Value.Overrides, Who, now);
        var added = await store.AppendOverrides(plan.Rows, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : new ProposeResult(opened.Value, plan.Rows.Count, plan.Existing, plan.Rejected);
    }
}
