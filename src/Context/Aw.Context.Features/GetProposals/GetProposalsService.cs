using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetProposals;

/// <summary>
/// Where each surface a scoping run proposed stands (spec §8.7), worked out against the current map at call time: mapped
/// (the map has it), new (only the latest draft has it) or unmapped (a person rejected it). The current map is read without
/// pending proposals, so a promoted proposal turns from new to mapped without running the scope again. A session sees only
/// the proposals of its own repo.
/// </summary>
internal sealed class GetProposalsService(IGraphStore store, IProposalRuns runs, IScrubberFactory scrubbers) : IService<GetProposalsRequest, ProposalsView>
{
    public async ValueTask<Result<ProposalsView>> Handle(GetProposalsRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var built = SettingsReader.Read(request.Scope.Settings).Bind(s => scrubbers.For(s.ExtraPatterns));
        var loaded = built.IsError ? built.Error : await Load(cancellationToken).ConfigureAwait(false);
        if (built.IsError || loaded.IsError)
        {
            return built.IsError ? built.Error : loaded.Error;
        }

        var (current, draft) = loaded.Value;
        var text = new MapText(built.Value, FencePolicies.For(request.Scope.Caller));
        var saved = await runs.Run(request.Run, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        var overrides = Visible(current.Overrides, request.Scope.Caller);
        var views = Views(current, draft, overrides, request.Scope);
        if (views.IsError)
        {
            return views.Error;
        }

        var (currentGraph, draftGraph) = views.Value;
        var statuses = GraphStatuses.Of(GraphStatuses.ForRun(request.Run, overrides, Visible(saved.Value, request.Scope.Caller)), currentGraph, draftGraph)
            .Select(s => s with { Surface = text.Label(s.Surface, Trust.Untrusted), Node = KeepNode(s, request.Scope.Caller, text) });
        return new ProposalsView(request.Run, current.Label, draft?.Label, [.. statuses]);
    }

    // Both maps as the caller may see them (MapAccess.View: the deny globs, and a session's repo for the current map). A session whose repo implements nothing sees an empty current map: every surface reads unmapped or new.
    private static Result<(GraphSnapshot Current, GraphSnapshot? Draft)> Views(MapSnapshot current, MapSnapshot? draft, IReadOnlyList<MapOverride> overrides, ContextScope scope)
    {
        var now = MapAccess.View(current.Graph, overrides, scope, pending: false);
        if (now.IsError && now.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return now.Error;
        }

        var next = draft is null ? null : MapAccess.View(draft.Graph, overrides, scope, pending: true, callerScope: false);
        return next is { IsError: true } ? next.Error : (now.IsError ? GraphSnapshot.Empty : now.Value.Applied, next?.Value.Applied);
    }

    // A new proposal's node key is built from the model-written name, so a fenced caller (a session) gets no key for it: the name is already there, fenced.
    // Every key goes out through the egress scrubber like the other reads' keys.
    private static NodeKey? KeepNode(GraphStatus status, ContextCaller caller, MapText text) =>
        status.Node is { } node && !(status.Status == GraphStatusKind.New && FencePolicies.For(caller) == FencePolicy.Fenced) && NodeKey.TryFrom(text.Line(node.Value), out var clean) ? clean : null;

    private async ValueTask<Result<(MapSnapshot Current, MapSnapshot? Draft)>> Load(CancellationToken cancellationToken)
    {
        var current = await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        if (current.Value is not { } map)
        {
            return ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>)");
        }

        var draft = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        return draft.IsError ? draft.Error : (map, draft.Value);
    }

    // A session sees the surfaces of its own repo's runs only.
    private static IReadOnlyList<ProposedSurface>? Visible(ScopeProposal? run, ContextCaller caller) =>
        run is null || (caller.Kind == ContextCallerKind.Session && run.Repo != caller.Repo) ? null : run.Surfaces;

    // A session sees the proposals of its own repo only.
    private static IReadOnlyList<MapOverride> Visible(IReadOnlyList<MapOverride> overrides, ContextCaller caller) =>
        caller.Kind != ContextCallerKind.Session
            ? overrides
            : [.. overrides.Where(o => o.Action != OverrideAction.Propose || (o.Proposal is { } p && caller.Repo is { } repo && p.Repo == repo))];
}
