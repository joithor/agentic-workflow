using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass of the session collector (collector spec §4.1): discovers the sessions, merges their states, publishes the snapshot and works out which changes are worth
/// a ledger row. It writes nothing but the in-memory snapshot and, after a good pass, prunes dead sessions' hook files.
/// </summary>
internal sealed partial class CollectSessionsService(SessionDiscovery discovery, SessionObserver observer, CollectorMemory memory, ISessionSnapshot snapshot, TimeProvider clock, ILogger<CollectSessionsService> logger)
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
        memory.Pending = Capped([.. memory.Pending, .. diff.Changes]);   // kept until the ledger has them (RecordFleetChangesService clears what it appended)
        memory.Previous = sessions.ToDictionary(static s => s.Ref);
        memory.Ended = ended;
        Publish(sessions, ended, found.Problems, now);
        return new CollectSessionsResponse(memory.Pending, Good: true, sessions.Count, discovery.Prune([.. sessions.Select(static s => s.Ref)], now));
    }

    /// <summary>A ledger that stays unwritable can't grow the queue without bound: past the cap the oldest changes are dropped, and the first drop is logged.</summary>
    private List<FleetChange> Capped(List<FleetChange> pending)
    {
        if (pending.Count <= CollectorMemory.PendingCap)
        {
            return pending;
        }

        if (!memory.PendingOverflowLogged)
        {
            memory.PendingOverflowLogged = true;
            LogOverflow(logger, CollectorMemory.PendingCap);
        }

        return pending[^CollectorMemory.PendingCap..];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session collector holds {Cap} fleet changes the ledger hasn't taken; the oldest are dropped from here on")]
    private static partial void LogOverflow(ILogger logger, int cap);

    private async Task<Discovered?> Discover(TimeSpan budget, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => discovery.Discover(budget, cancellationToken), cancellationToken).ConfigureAwait(false);   // null: the process table overran
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
