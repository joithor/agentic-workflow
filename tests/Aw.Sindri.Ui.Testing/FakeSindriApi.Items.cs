using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The P3 routes (spec-shaped until P3's goldens land): the queue, an item, its timeline, CreateItem and CloseItem.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<CreateItemRequest> _created = [];
    private readonly List<(string Item, CloseItemRequest Request)> _closed = [];
    private int _queueCalls;
    private int _itemCalls;
    private int _timelineCalls;

    /// <summary>GET /sindri/queue's answer.</summary>
    public RouteAnswer QueueView { get; set; } = RouteAnswer.SpecShaped("queue", "fixture");

    /// <summary>GET /sindri/items/{id}'s answer per id; an id not here answers SND-ITEM-404.</summary>
    public Dictionary<string, RouteAnswer> Items { get; } = new(StringComparer.Ordinal)
    {
        [A.NewItem] = RouteAnswer.SpecShaped("items-id", "new"),
        [A.DecisionItem] = RouteAnswer.SpecShaped("items-id", "extra-p4-needs-decision"),
    };

    /// <summary>GET /sindri/items/{id}/timeline's page per 'after' (any item).</summary>
    public Dictionary<long, RouteAnswer> TimelinePages { get; } = new()
    {
        [0] = RouteAnswer.SpecShaped("items-id-timeline", "fixture"),
        [151] = RouteAnswer.SpecShaped("items-id-timeline", "page-2"),
    };

    /// <summary>Held open until set, per item id: that item's read waits for its gate (others answer at once).</summary>
    public Dictionary<string, TaskCompletionSource> ItemGates { get; } = new(StringComparer.Ordinal);

    /// <summary>Held open until set, per 'after' cursor: that timeline page's read waits for its gate.</summary>
    public Dictionary<long, TaskCompletionSource> TimelineGates { get; } = [];

    /// <summary>POST /sindri/items' answer.</summary>
    public RouteAnswer Create { get; set; } = RouteAnswer.SpecShaped("items", "created");

    /// <summary>POST /sindri/items/{id}/close's answer.</summary>
    public RouteAnswer Close { get; set; } = RouteAnswer.SpecShaped("items-id-close", "accepted");

    /// <summary>Held open until set, to see a command's sending phase.</summary>
    public TaskCompletionSource? CommandGate { get; set; }

    /// <summary>Runs after an accepted CloseItem, as the projector would move the item (a test sets the next answers here).</summary>
    public Action<string, CloseItemRequest>? OnClosed { get; set; }

    /// <summary>Every CreateItem body received, in order.</summary>
    public IReadOnlyList<CreateItemRequest> Created { get { lock (_gate) { return [.. _created]; } } }

    /// <summary>Every CloseItem received (item, body), in order.</summary>
    public IReadOnlyList<(string Item, CloseItemRequest Request)> Closed { get { lock (_gate) { return [.. _closed]; } } }

    /// <summary>How many times the queue was read.</summary>
    public int QueueCalls => Volatile.Read(ref _queueCalls);

    /// <summary>How many times an item was read.</summary>
    public int ItemCalls => Volatile.Read(ref _itemCalls);

    /// <summary>How many timeline pages were read.</summary>
    public int TimelineCalls => Volatile.Read(ref _timelineCalls);

    /// <summary>The answer for one item id (the handler serves the same text).</summary>
    /// <param name="itemId">The id.</param>
    /// <returns>Its answer, or the SND-ITEM-404 golden.</returns>
    public RouteAnswer ItemAnswer(string itemId) =>
        Items.TryGetValue(itemId, out var answer) ? answer : RouteAnswer.Problem("items-id", "not-found");

    /// <summary>The page for one cursor; a cursor with no page answers an empty last page.</summary>
    /// <param name="after">The cursor.</param>
    /// <returns>The page's answer.</returns>
    public RouteAnswer TimelineAnswer(long after) =>
        TimelinePages.TryGetValue(after, out var page) ? page
            : new RouteAnswer(System.Net.HttpStatusCode.OK, """{"entries":[],"next":null,"asOf":{"cursor":57,"ledgerHead":57,"projectedAt":null,"rebuilding":false,"stalled":false}}""");

    public Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _queueCalls);
        return Task.FromResult(Answer<QueueView>(api, "/sindri/queue", QueueView));
    }

    public async Task<Result<ItemView>> Item(Uri api, string itemId, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _itemCalls);
        var answer = ItemAnswer(itemId);
        if (ItemGates.TryGetValue(itemId, out var gate))
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ItemView>(api, "/sindri/items/{id}", answer);
    }

    public async Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _timelineCalls);
        var answer = TimelineAnswer(after);
        if (TimelineGates.TryGetValue(after, out var gate))
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<TimelineView>(api, "/sindri/items/{id}/timeline", answer);
    }

    public async Task<Result<ItemCreatedView>> CreateItem(Uri api, CreateItemRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _created.Add(request);
        }

        if (CommandGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ItemCreatedView>(api, "/sindri/items", Create);
    }

    public async Task<Result<CommandAcceptedView>> CloseItem(Uri api, string itemId, CloseItemRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _closed.Add((itemId, request));
        }

        if (CommandGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var answer = Answer<CommandAcceptedView>(api, "/sindri/items/{id}/close", Close);
        if (answer.IsSuccess)
        {
            OnClosed?.Invoke(itemId, request);
        }

        return answer;
    }
}
