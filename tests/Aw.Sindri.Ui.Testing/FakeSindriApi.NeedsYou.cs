using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Needs you routes (API P4's goldens): the list, the decisions, the requests, Decide and FileRequest.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Decision, DecideRequest Request)> _decided = [];
    private readonly List<FileRequestRequest> _filed = [];
    private readonly List<(bool? Pending, string? Item)> _decisionQueries = [];
    private readonly List<(string? State, string? Item)> _requestQueries = [];
    private int _needsYouCalls;

    /// <summary>GET /sindri/needs-you's answer.</summary>
    public RouteAnswer NeedsYouView { get; set; } = RouteAnswer.Api("needs-you", "fixture");

    /// <summary>GET /sindri/decisions's answer for pending=true (and for no filter).</summary>
    public RouteAnswer PendingDecisions { get; set; } = RouteAnswer.Api("decisions", "pending");

    /// <summary>GET /sindri/decisions's answer for pending=false.</summary>
    public RouteAnswer DecidedDecisions { get; set; } = RouteAnswer.Api("decisions", "decided");

    /// <summary>GET /sindri/decisions's answer per item id (an item without one has no decisions).</summary>
    public Dictionary<string, RouteAnswer> DecisionsByItem { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/requests's answer.</summary>
    public RouteAnswer RequestsView { get; set; } = RouteAnswer.Api("requests", "list-fixture");

    /// <summary>POST /sindri/decisions/{id}'s answer (any decision without its own in <see cref="DecideAnswers"/>).</summary>
    public RouteAnswer Decided { get; set; } = RouteAnswer.Api("decisions-id", "accepted");

    /// <summary>POST /sindri/decisions/{id}'s answer per decision id.</summary>
    public Dictionary<string, RouteAnswer> DecideAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>POST /sindri/requests's answer.</summary>
    public RouteAnswer Filed { get; set; } = RouteAnswer.Api("requests", "filed");

    /// <summary>Held open until set: the needs-you read waits for it.</summary>
    public TaskCompletionSource? NeedsYouGate { get; set; }

    /// <summary>How many needs-you reads were made.</summary>
    public int NeedsYouCalls => Volatile.Read(ref _needsYouCalls);

    /// <summary>Every Decide received (decision, body), in order.</summary>
    public IReadOnlyList<(string Decision, DecideRequest Request)> DecideCalls { get { lock (_gate) { return [.. _decided]; } } }

    /// <summary>Every FileRequest body received, in order.</summary>
    public IReadOnlyList<FileRequestRequest> FileCalls { get { lock (_gate) { return [.. _filed]; } } }

    /// <summary>Every decisions query received (pending, item), in order.</summary>
    public IReadOnlyList<(bool? Pending, string? Item)> DecisionQueries { get { lock (_gate) { return [.. _decisionQueries]; } } }

    /// <summary>Every requests query received (state, item), in order.</summary>
    public IReadOnlyList<(string? State, string? Item)> RequestQueries { get { lock (_gate) { return [.. _requestQueries]; } } }

    /// <summary>Runs after an accepted Decide, as the projector would move the rows (a test sets the next answers here).</summary>
    public Action<string, DecideRequest>? OnDecided { get; set; }

    /// <summary>The decisions answer for one query.</summary>
    /// <param name="pending">The pending filter.</param>
    /// <param name="item">The item filter.</param>
    /// <returns>The answer.</returns>
    public RouteAnswer DecisionsAnswer(bool? pending, string? item) =>
        item is not null ? DecisionsByItem.GetValueOrDefault(item, RouteAnswer.Api("decisions", "empty")) : pending == false ? DecidedDecisions : PendingDecisions;

    /// <summary>GET /sindri/requests's answer per item id (an item without one has no requests).</summary>
    public Dictionary<string, RouteAnswer> RequestsByItem { get; } = new(StringComparer.Ordinal);

    /// <summary>The requests answer for one query.</summary>
    /// <param name="item">The item filter.</param>
    /// <returns>The answer.</returns>
    public RouteAnswer RequestsAnswer(string? item) =>
        item is not null ? RequestsByItem.GetValueOrDefault(item, RouteAnswer.Api("requests", "list-empty")) : RequestsView;

    /// <summary>The answer to Decide for one decision.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>The answer.</returns>
    public RouteAnswer DecideAnswer(string decisionId) => DecideAnswers.TryGetValue(decisionId, out var own) ? own : Decided;

    public Task<Result<NeedsYouList>> NeedsYou(Uri api, CancellationToken cancellationToken) => NeedsYou(api, sequence: null, cancellationToken);

    /// <summary>A needs-you read that carries <paramref name="sequence"/> as X-Sindri-After.</summary>
    /// <param name="api">The API.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<NeedsYouList>> NeedsYou(Uri api, long? sequence, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _needsYouCalls);
        return View<NeedsYouList>(api, "/sindri/needs-you", NeedsYouView, sequence, NeedsYouGate, cancellationToken);
    }

    public Task<Result<DecisionList>> Decisions(Uri api, bool? pending, string? item, CancellationToken cancellationToken) =>
        Decisions(api, pending, item, sequence: null, cancellationToken);

    /// <summary>A decisions read that carries <paramref name="sequence"/> as X-Sindri-After.</summary>
    /// <param name="api">The API.</param>
    /// <param name="pending">The pending filter.</param>
    /// <param name="item">The item filter.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<DecisionList>> Decisions(Uri api, bool? pending, string? item, long? sequence, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _decisionQueries.Add((pending, item));
        }

        return View<DecisionList>(api, "/sindri/decisions", DecisionsAnswer(pending, item), sequence, gate: null, cancellationToken);
    }

    public Task<Result<RequestList>> Requests(Uri api, string? state, string? item, CancellationToken cancellationToken) =>
        Requests(api, state, item, sequence: null, cancellationToken);

    /// <summary>A requests read that carries <paramref name="sequence"/> as X-Sindri-After.</summary>
    /// <param name="api">The API.</param>
    /// <param name="state">The state filter.</param>
    /// <param name="item">The item filter.</param>
    /// <param name="sequence">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<RequestList>> Requests(Uri api, string? state, string? item, long? sequence, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _requestQueries.Add((state, item));
        }

        return View<RequestList>(api, "/sindri/requests", RequestsAnswer(item), sequence, gate: null, cancellationToken);
    }

    public async Task<Result<CommandAcceptedView>> Decide(Uri api, string decisionId, DecideRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<CommandAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _decided.Add((decisionId, request));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<CommandAcceptedView>(api, "/sindri/decisions/{id}", DecideAnswer(decisionId));
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Sequence);
            OnDecided?.Invoke(decisionId, request);
        }

        return answer;
    }

    public async Task<Result<RequestFiledView>> FileRequest(Uri api, FileRequestRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<RequestFiledView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _filed.Add(request);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<RequestFiledView>(api, "/sindri/requests", Filed);
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Accepted.Sequence);
        }

        return answer;
    }
}
