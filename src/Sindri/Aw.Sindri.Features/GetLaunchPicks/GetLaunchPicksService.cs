using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>
/// Reads the model picks from the ledger (model-pick spec §6, §6.1): the 'model.picking' and 'model.picked' rows of the item or
/// instance, oldest first, each with its run's last 'session.usage' sample (priced with <see cref="Budgets.PriceOf"/>) and how the
/// run ended. A run ends in the instance's own rows: the row that opens it names the launch as the frame's actor and the run's
/// token, and the 'step.completed' or 'step.failed' row of that token closes it, in order, so a retry on the same token pairs
/// its own launch. A run with no closing row is running; 'launch.failed' alone makes it failed.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
/// <param name="items">Resolves an item named by alias.</param>
/// <param name="repos">Resolves an item named by id, from the ledger.</param>
/// <param name="callers">The caller.</param>
/// <param name="history">The model history, for the escalation rates.</param>
internal sealed class GetLaunchPicksService(ILedgerReader ledger, EventFactory events, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers, IModelHistory history)
    : IService<GetLaunchPicksRequest, LaunchPicks>
{
    private const int Page = 500;

    private static readonly string[] _types = [EventTypes.ModelPicking, EventTypes.ModelPicked, EventTypes.SessionUsage, EventTypes.LaunchFailed];

    public async ValueTask<Result<LaunchPicks>> Handle(GetLaunchPicksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemFilters.Resolve(request.Item, items, repos, callers, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var scanned = await ScanAll(new LedgerFilter(TypeFilter.Parse(string.Join(',', _types)).Value, Item: null), from: 0, cancellationToken).ConfigureAwait(false);
        if (scanned.IsError)
        {
            return scanned.Error;
        }

        var read = new PickRows(events, item.Value, request.Instance);
        foreach (var row in scanned.Value)
        {
            read.Add(row);
        }

        var picks = read.Picks;
        var ended = await Ended(picks, cancellationToken).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        var rates = await Rates(picks, cancellationToken).ConfigureAwait(false);
        return rates.IsError ? rates.Error : new LaunchPicks(View(picks, read, ended.Value), rates.Value);
    }

    /// <summary>For each state and model the picks used, how often that model was retried on a stronger one over its last 20 runs (from the model history); none when it has no counted run.</summary>
    private async ValueTask<Result<List<StateEscalation>>> Rates(IReadOnlyList<PickEntry> picks, CancellationToken cancellationToken)
    {
        var rates = new List<StateEscalation>();
        var wanted = picks.Where(static p => p.Picked?.Model is not null).Select(static p => (p.Instance, p.State, Model: p.Picked!.Model!)).Distinct();   // why: the filter keeps only picks with a model
        foreach (var (instance, state, model) in wanted)
        {
            var scope = await history.ScopeOf(WorkflowInstanceId.Of(instance), cancellationToken).ConfigureAwait(false);
            if (scope.IsError)
            {
                return scope.Error;
            }

            if (scope.Value is not { } known)
            {
                continue;
            }

            var rows = await history.For(known.Recipe, known.Version, state, [model], cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return rows.Error;
            }

            var counted = rows.Value[0].Recent.Count(static o => o != ModelOutcome.Crashed);
            if (counted > 0 && !rates.Exists(r => string.Equals(r.State, state, StringComparison.Ordinal) && string.Equals(r.Model, model, StringComparison.Ordinal)))
            {
                rates.Add(new StateEscalation(state, model, counted, rows.Value[0].Recent.Count(static o => o is ModelOutcome.Refused or ModelOutcome.CheckFailed), rows.Value[0].EscalationRate));
            }
        }

        return rates;
    }

    /// <summary>How each launch ended, from the instances' own rows after the first pick.</summary>
    private async ValueTask<Result<Dictionary<LaunchId, string>>> Ended(IReadOnlyList<PickEntry> picks, CancellationToken cancellationToken)
    {
        var pairing = new RunPairing(events);
        var from = picks.Count == 0 ? 0 : Math.Max(picks[0].Sequence - 1, 0);
        foreach (var instance in picks.Select(static p => p.Instance).Distinct())
        {
            var rows = await ScanAll(new LedgerFilter(Types: null, Item: null, Instance: instance), from, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return rows.Error;
            }

            foreach (var row in rows.Value)
            {
                pairing.Add(row);
            }
        }

        return pairing.Ended;
    }

    private static List<LaunchPickView> View(IReadOnlyList<PickEntry> picks, PickRows read, Dictionary<LaunchId, string> ended)
    {
        var escalations = new Dictionary<(string Instance, string State, int Attempt), int>();
        var views = new List<LaunchPickView>();
        foreach (var entry in picks)
        {
            var source = (Instance: entry.Instance.Value, entry.State, entry.Attempt);
            var escalation = escalations.GetValueOrDefault(source);
            escalations[source] = escalation + 1;
            views.Add(Row(entry, escalation, read.Usage.GetValueOrDefault(entry.Launch), Outcome(entry.Launch, ended, read.LaunchFailed)));
        }

        return views;
    }

    private static string Outcome(LaunchId launch, Dictionary<LaunchId, string> ended, HashSet<LaunchId> failed) =>
        ended.TryGetValue(launch, out var word) ? word : failed.Contains(launch) ? LaunchPickOutcomes.Failed : LaunchPickOutcomes.Running;

    private static LaunchPickView Row(PickEntry entry, int escalation, SessionUsage? sample, string outcome) =>
        new(
            entry.Launch,
            entry.Item,
            entry.Instance,
            entry.State,
            entry.Attempt,
            escalation,
            Picking: entry.Picked is null,
            entry.Picked,
            sample?.InputTokens ?? 0,
            sample?.OutputTokens ?? 0,
            Cost(sample, entry.Picked),
            outcome,
            entry.Picked is { } picked ? PickWhy.Sentence(picked) : PickWhy.Asking,
            entry.At);

    /// <summary>The run's tokens priced; null when no sample was taken or the model has no price.</summary>
    private static decimal? Cost(SessionUsage? sample, ModelPicked? picked)
    {
        if (sample is null || (sample.Model ?? picked?.Model) is not { } model)
        {
            return null;
        }

        var cacheRead = sample.CacheReadTokens ?? 0;
        var spend = Budgets.Spend([new ModelTokens(model, sample.InputTokens - cacheRead, cacheRead, 0, sample.OutputTokens)]);
        return spend.Basis == SpendBasis.PriceTable ? spend.Usd : null;
    }

    private async ValueTask<Result<List<LedgerEvent>>> ScanAll(LedgerFilter filter, long from, CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var found = new List<LedgerEvent>();
        var cursor = from;
        while (cursor < head.Value)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            found.AddRange(scan.Value.Events);
            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return found;
    }
}
