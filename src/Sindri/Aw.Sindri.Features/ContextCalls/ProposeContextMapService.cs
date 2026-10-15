using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Writes a scoping run's surfaces to the latest draft (context spec §8.5; README X63): the engine adds 'propose' rows (idempotent,
/// never again after a reject, never the current map), and 'context.map.proposed' is appended when something was added. A job-class
/// command run synchronously: it is fast, and the app refreshes on the event.
/// </summary>
internal sealed class ProposeContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
    : IService<ProposeContextMapRequest, ProposeContextMapResponse>
{
    private const int MaxRun = 200;

    public async ValueTask<Result<ProposeContextMapResponse>> Handle(ProposeContextMapRequest request, CancellationToken cancellationToken)
    {
        var proposal = Read(request);
        if (proposal.IsError)
        {
            return proposal.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var proposed = await engine.Propose(proposal.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        if (proposed.IsError)
        {
            return proposed.Error;
        }

        var standing = await engine.Proposals(proposal.Value.Run, scope.Value, cancellationToken).ConfigureAwait(false);
        if (standing.IsError)
        {
            return standing.Error;
        }

        return proposed.Value.Added == 0
            ? new ProposeContextMapResponse(proposed.Value, Sequence: null, standing.Value.Surfaces)
            : await Record(proposal.Value, proposed.Value, standing.Value.Surfaces, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ScopeProposal> Read(ProposeContextMapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Run) || request.Run.Length > MaxRun)
        {
            return SindriErrors.InvalidInput.With($"run: the scoping run's id is 1 to {MaxRun} characters");
        }

        return ContextRepoName.TryFrom(request.Repo, out var repo)
            ? new ScopeProposal(request.Run, repo, request.Surfaces ?? [])
            : ContextErrors.RepoOutsideScope.With($"'{request.Repo}' is not a repo this scope can see (fix: name a repo of the approved profile)");
    }

    private async ValueTask<Result<ProposeContextMapResponse>> Record(ScopeProposal proposal, ProposeResult result, IReadOnlyList<GraphStatus> surfaces, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var payload = new ContextMapProposed(proposal.Run, proposal.Repo.Value, result.Draft.Number, result.Added, result.Existing, result.Rejected);
        var call = callers.Current is { } caller ? caller.CorrelationId.Value.ToString("N") : Guid.NewGuid().ToString("N");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapProposed, payload, $"{EventTypes.ContextMapProposed}:{call}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProposeContextMapResponse(result, appended.Value.Sequence, surfaces);
    }
}
