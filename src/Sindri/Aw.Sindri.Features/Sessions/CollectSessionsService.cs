using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass of the session collector (collector spec §4.1): discovers the sessions, merges their states, publishes the snapshot and works out which changes are worth
/// a ledger row. It writes nothing but the in-memory snapshot and, after a good pass, prunes dead sessions' hook files.
/// </summary>
internal sealed class CollectSessionsService(SessionDiscovery discovery, SessionObserver observer, CollectorMemory memory, ISessionSnapshot snapshot, TimeProvider clock)
    : IService<CollectSessionsRequest, CollectSessionsResponse>
{
    public async ValueTask<Result<CollectSessionsResponse>> Handle(CollectSessionsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        if (!discovery.IsEnabled())
        {
            Publish([], [], [SessionProblems.Disabled], now);
            return new CollectSessionsResponse(memory.Pending, Good: false, Sessions: 0, Pruned: 0);
        }

        var found = await Discover(request.Budget, cancellationToken).ConfigureAwait(false);
        if (found is null || !found.Good)
        {
            KeepPrevious([.. found?.Problems ?? [SessionProblems.Overrun]], now);
            return new CollectSessionsResponse(memory.Pending, Good: false, Sessions: 0, Pruned: 0) { Overran = found is null };
        }

        var sessions = Observe(found.Candidates, now);
        var diff = FleetChangeRules.Diff(memory.Tracker, sessions, now, goodPass: true);
        var ended = memory.Ending(sessions, now);
        memory.Tracker = diff.State;
        memory.Pending = [.. memory.Pending, .. diff.Changes];   // kept until the ledger has them (RecordFleetChangesService clears what it appended)
        memory.Previous = sessions.ToDictionary(static s => s.Ref);
        memory.Ended = ended;
        Publish(sessions, ended, found.Problems, now);
        return new CollectSessionsResponse(memory.Pending, Good: true, sessions.Count, discovery.Prune([.. sessions.Select(static s => s.Ref)], now));
    }

    private async Task<Discovered?> Discover(TimeSpan budget, CancellationToken cancellationToken)
    {
        using var within = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        within.CancelAfter(budget);
        try
        {
            return await Task.Run(() => discovery.Discover(within.Token), within.Token).WaitAsync(within.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;   // overran its budget
        }
        catch (OperationCanceledException)
        {
            return new Discovered([], [], Good: false);   // shutting down: no pass, no problem row, nothing published as a failure
        }
    }

    private List<ObservedSession> Observe(IReadOnlyList<Candidate> candidates, DateTimeOffset now) =>
    [
        .. candidates.GroupBy(static c => c.Ref).Select(static g => g.First())
            .Select(c => observer.Observe(c, now, memory.Previous.GetValueOrDefault(c.Ref)))
            .OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal),
    ];

    private void KeepPrevious(List<SessionProblem> problems, DateTimeOffset now)
    {
        var last = snapshot.Current;
        Publish(last?.Sessions ?? [], last?.RecentlyEnded ?? [], problems, last?.ObservedAt ?? now);
    }

    private void Publish(IReadOnlyList<ObservedSession> sessions, IReadOnlyList<EndedSession> ended, IReadOnlyList<SessionProblem> problems, DateTimeOffset at) =>
        snapshot.Publish(new SessionSnapshot(sessions, ended, [.. discovery.StaticProblems(), .. problems], at));
}
