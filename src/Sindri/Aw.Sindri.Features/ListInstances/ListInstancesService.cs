using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>
/// Finds the instances by their search attributes (engine spec §5.1) and asks each for its view, so every line is a
/// full status header. '--needs-me' is served here by a visibility query on 'NeedsYou = true', shaped like API's
/// 'GET /needs-you' (Decisions, then Tasks; README X5) until API P4 builds that route, which it then reads instead.
/// The engine has no item titles: they come from the items view (API P3), one keyed read per distinct item of the list, fed to the row, the order and
/// the subject filter, so the label a row shows is the one it sorts by. An unreadable view degrades to the item's id. A child that has not
/// run yet reads QUEUED from its starting state; nothing in this list knows a child that was never started, so engine P7 wires 'NotStarted'.
/// </summary>
/// <param name="directory">Resolves an address or a bare item ('address').</param>
/// <param name="instances">The visibility query.</param>
/// <param name="runtime">Each instance's 'GetInstance' view (a Query for a running one, remembered for an ended one); one that ended between the list and its query (SND-WF-001) is left out.</param>
/// <param name="itemViews">The 'items' view, read by key for the titles of the items the instances are on.</param>
/// <param name="repos">Where each item belongs, from the ledger: a session lists only its own repo's instances.</param>
/// <param name="callers">The caller, whose repo scope decides which instances and item titles it may see.</param>
/// <param name="rowBuilder">Builds the rows at the time the list is read: their ages and timeouts are worked out from it, not from when the workflow last ran, and each carries its recipe's display name.</param>
internal sealed partial class ListInstancesService(IInstanceDirectory directory, IWorkflowInstances instances, EndedInstanceViews runtime, IViewReader itemViews, IItemRepos repos, ICallerContextAccessor callers, InstanceRows rowBuilder) : IService<ListInstancesRequest, InstanceList>
{
    public async ValueTask<Result<InstanceList>> Handle(ListInstancesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Address is { } reference)
        {
            return await One(reference, cancellationToken).ConfigureAwait(false);
        }

        if (Refusal(request) is { } refused)
        {
            return refused;
        }

        var open = Flag(request.Open) == true;
        var filter = Filter(request);
        var rows = request.Query is { Length: > 0 } query
            ? await instances.List(Joined(InstanceFilters.ToQuery(filter), query), ended: !open, cancellationToken).ConfigureAwait(false)
            : await instances.List(filter, ended: !open, cancellationToken).ConfigureAwait(false);
        var visible = rows.IsError ? rows.Error : await Scoped(rows.Value, cancellationToken).ConfigureAwait(false);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var known = new Dictionary<string, InstanceView>(StringComparer.Ordinal);
        var views = await Views(visible.Value, known, cancellationToken).ConfigureAwait(false);
        if (views.IsError)
        {
            return views.Error;
        }

        var counts = await RootCounts(known, cancellationToken).ConfigureAwait(false);   // over the root set, before 'recipe' or 'open' narrows the rows
        if (counts.IsError)
        {
            return counts.Error;
        }

        var unfinished = open ? views.Value.FindAll(static v => v.Outcome is null) : views.Value;
        var kept = Flag(request.NeedsMe) == true ? [.. NeedsYouList.Shown(unfinished)] : unfinished;
        var titles = await Titles(kept, cancellationToken).ConfigureAwait(false);
        return Listed(Matching(kept, request.Subject, titles), counts.Value, titles, Empty(request));
    }

    /// <summary>
    /// The counts of every root instance, ended ones included: the root set listed twice, all and running only. A root that is no longer running
    /// is finished (its chip is DONE) and is counted from the listing; only the running ones are asked for their view, reusing the views the first call read.
    /// </summary>
    private async ValueTask<Result<ListCounts>> RootCounts(Dictionary<string, InstanceView> known, CancellationToken cancellationToken)
    {
        var all = await instances.List(new InstanceFilter(Root: true), ended: true, cancellationToken).ConfigureAwait(false);
        var running = all.IsError ? all.Error : await instances.List(new InstanceFilter(Root: true), ended: false, cancellationToken).ConfigureAwait(false);
        var visible = running.IsError ? running.Error : await Scoped(all.Value, cancellationToken).ConfigureAwait(false);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var live = running.Value.Select(static r => r.Address).ToHashSet(StringComparer.Ordinal);
        var open = visible.Value.Where(r => live.Contains(r.Address)).ToList();
        var finished = visible.Value.Count(r => !live.Contains(r.Address) && InstanceAddress.TryFrom(r.Address, out _));
        var views = await Views(open, known, cancellationToken).ConfigureAwait(false);
        return views.IsError ? views.Error : InstanceCounts.Of(views.Value) with { Done = InstanceCounts.Of(views.Value).Done + finished };
    }

    /// <summary>The rows the caller may read: all of them, or for a session its own repo's. No caller means the service is called inside the process, with no scope to apply.</summary>
    private async ValueTask<Result<IReadOnlyList<InstanceRow>>> Scoped(IReadOnlyList<InstanceRow> rows, CancellationToken cancellationToken) =>
        callers.Current is { } caller ? await InstanceScope.Visible(caller, rows, repos, cancellationToken).ConfigureAwait(false) : Results.Success(rows);

    /// <summary>Each row's 'GetInstance' view (read once per address through <paramref name="known"/>); one that ended between the list and its query is left out.</summary>
    private async ValueTask<Result<List<InstanceView>>> Views(IReadOnlyList<InstanceRow> rows, Dictionary<string, InstanceView> known, CancellationToken cancellationToken)
    {
        var views = new List<InstanceView>();
        foreach (var row in rows)
        {
            if (!InstanceAddress.TryFrom(row.Address, out var address))
            {
                continue;   // not an instance address: no RecipeWorkflow of this build started it
            }

            if (known.TryGetValue(address.Value, out var cached))
            {
                views.Add(cached);
                continue;
            }

            var view = await runtime.Get(row, address, cancellationToken).ConfigureAwait(false);
            if (view.IsSuccess)
            {
                known[address.Value] = view.Value;
                views.Add(view.Value);
            }
            else if (view.Error.Code != WorkflowErrors.InstanceNotFound.Code)
            {
                return view.Error;
            }
        }

        return views;
    }

    /// <summary>The filter value that isn't one of its words, with the fix; null when they are fine.</summary>
    private static Error? Refusal(ListInstancesRequest request)
    {
        if (request.State is { } state && !StateName.IsMatch(state))
        {
            return SindriErrors.InvalidInput.With($"'{state}' isn't a state name (fix: --state <name>, e.g. --state produce or --state parked)");
        }

        if (request.Urgency is { } urgency && !Urgencies.IsKnown(urgency))
        {
            return SindriErrors.InvalidInput.With($"'{urgency}' isn't an urgency (fix: urgency incident, triage or work)");
        }

        return NotTrueOrFalse(request.NeedsMe, "needsMe", "true for what needs you")
            ?? NotTrueOrFalse(request.Root, "root", "true for top-level instances, false for children")
            ?? NotTrueOrFalse(request.Open, "open", "true for the unfinished instances only");
    }

    /// <summary>
    /// The one instance an address or bare item names, as a one-row list (its id included). An id lookup is not a list, so its counts cover
    /// the row alone, not the root set.
    /// </summary>
    private async ValueTask<Result<InstanceList>> One(string reference, CancellationToken cancellationToken)
    {
        var address = await directory.Resolve(reference, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (callers.Current is { } caller && await InstanceScope.Refusal(caller, view.Value.ItemId, reference, repos, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        return Listed([view.Value], InstanceCounts.Of([view.Value]), await Titles([view.Value], cancellationToken).ConfigureAwait(false), empty: null);
    }

    /// <summary>
    /// The views whose subject, as the row shows it (<see cref="InstanceSummaries.SubjectOf"/>: an item's title, an instance with no subject
    /// its address), has the key <paramref name="subject"/> or a label containing it; all of them when none is asked for. A label match
    /// can't be a search attribute (a keyword has no substring match), so it is made here.
    /// </summary>
    private static List<InstanceView> Matching(List<InstanceView> views, string? subject, ItemTitles titles) =>
        subject is null
            ? views
            : [.. views.Where(v => InstanceSummaries.SubjectOf(v, titles.For(v)) is var s && (string.Equals(s.Key, subject, StringComparison.Ordinal) || s.Label.Contains(subject, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>The list: the rows in the API order (§3.8: one order on every list path), each built by <see cref="InstanceSummaries.Of"/>.</summary>
    private InstanceList Listed(List<InstanceView> views, ListCounts counts, ItemTitles titles, string? empty)
    {
        var now = rowBuilder.Now();
        var list = views.Order(new InstanceOrder(titles)).Select(v => rowBuilder.Row(v, titles, now)).ToList();
        return new InstanceList(list, counts, list.Count == 0 ? empty : null, now);
    }

    /// <summary>
    /// The titles of the items <paramref name="views"/> are on that the caller may see: one keyed read of the 'items' view per distinct item
    /// (none when no view has one). With no caller, or an item whose read fails or is out of the caller's scope, the item is labelled by its id.
    /// </summary>
    private async ValueTask<ItemTitles> Titles(IReadOnlyList<InstanceView> views, CancellationToken cancellationToken)
    {
        if (callers.Current is not { } caller)
        {
            return ItemTitles.None;
        }

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in views.Select(static v => v.ItemId).OfType<ItemKey>().Select(static i => i.Value).Distinct(StringComparer.Ordinal))
        {
            var row = await itemViews.Get<ItemRow>(ViewNames.Items, id, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { } item && ItemScope.Visible(caller, item.Repo))
            {
                titles[id] = item.Title;
            }
        }

        return new ItemTitles(titles);
    }

    /// <summary>The sentence for an empty list that matches the filter: nothing needs you, no open workflows, or no workflow matches.</summary>
    private static string Empty(ListInstancesRequest request) =>
        Flag(request.NeedsMe) == true ? NeedsYouList.Nothing
        : Flag(request.Open) == true ? InstanceList.EmptyText
        : InstanceList.NoMatchText;

    private static Error? NotTrueOrFalse(string? value, string name, string meaning) =>
        value is not null && Flag(value) is null
            ? SindriErrors.InvalidInput.With($"{name}='{value}' isn't true or false (fix: {name}=true or {name}=false; {meaning})")
            : null;

    private static bool? Flag(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? true
        : string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? false
        : null;

    private static InstanceFilter Filter(ListInstancesRequest request) => new(
        request.Recipe,
        request.State,
        Flag(request.NeedsMe) == true ? true : null,
        request.Urgency is { } urgency ? Urgencies.Parse(urgency) : null,
        request.Item?.Value,
        Subject: null,   // why: matched on the views (Matching): a search attribute can match the key but not a part of the label
        Root: Flag(request.Root));

    /// <summary>The typed filters' query, ANDed with the caller's raw one.</summary>
    private static string Joined(string? typed, string query) => typed is null ? $"({query})" : $"{typed} AND ({query})";

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StateName { get; }
}
