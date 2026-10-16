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
/// <param name="runtime">Each instance's 'GetInstance' Query; one that ended between the list and its query (SND-WF-001) is left out.</param>
/// <param name="itemViews">The 'items' view, read by key for the titles of the items the instances are on.</param>
/// <param name="callers">The caller, whose repo scope decides which item titles it may see.</param>
/// <param name="clock">The time the rows are read at: their ages and timeouts are worked out from it, not from when the workflow last ran.</param>
internal sealed partial class ListInstancesService(IInstanceDirectory directory, IWorkflowInstances instances, IInstanceReader runtime, IViewReader itemViews, ICallerContextAccessor callers, TimeProvider clock) : IService<ListInstancesRequest, ListInstancesResponse>
{
    public async ValueTask<Result<ListInstancesResponse>> Handle(ListInstancesRequest request, CancellationToken cancellationToken)
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

        var needsMe = Flag(request.NeedsMe) == true;
        var filter = Filter(request);
        var ended = request.Item is not null || request.State is not null;
        var rows = request.Query is { Length: > 0 } query
            ? await instances.List(Joined(InstanceFilters.ToQuery(filter), query), ended, cancellationToken).ConfigureAwait(false)
            : await instances.List(filter, ended, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var views = await Views(rows.Value, cancellationToken).ConfigureAwait(false);
        if (views.IsError)
        {
            return views.Error;
        }

        var titles = await Titles(views.Value, cancellationToken).ConfigureAwait(false);
        return Listed(needsMe, Matching(views.Value, request.Subject, titles), titles);
    }

    /// <summary>Each row's 'GetInstance' view; one that ended between the list and its query is left out.</summary>
    private async ValueTask<Result<List<InstanceView>>> Views(IReadOnlyList<InstanceRow> rows, CancellationToken cancellationToken)
    {
        var views = new List<InstanceView>();
        foreach (var row in rows)
        {
            if (!InstanceAddress.TryFrom(row.Address, out var address))
            {
                continue;   // not an instance address: no RecipeWorkflow of this build started it
            }

            var view = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
            if (view.IsSuccess)
            {
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

        return NotTrueOrFalse(request.NeedsMe, "needsMe", "true for what needs you") ?? NotTrueOrFalse(request.Root, "root", "true for top-level instances, false for children");
    }

    /// <summary>The one instance an address or bare item names, as 'GetInstance' shows it (its id included).</summary>
    private async ValueTask<Result<ListInstancesResponse>> One(string reference, CancellationToken cancellationToken)
    {
        var address = await directory.Resolve(reference, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : Rows(new ListInstancesResponse([view.Value], NeedsYou: null, Empty: null), await Titles([view.Value], cancellationToken).ConfigureAwait(false));
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

    private ListInstancesResponse Listed(bool needsMe, List<InstanceView> views, ItemTitles titles)
    {
        if (needsMe)
        {
            var sections = NeedsYouList.Of(views);
            return Rows(new([.. sections.Decisions, .. sections.Tasks], sections, sections.IsEmpty ? NeedsYouList.Nothing : null), titles);
        }

        var sorted = views.Order(new InstanceOrder(titles)).ToList();   // API spec §3.8: one order on every list path
        return Rows(new(sorted, NeedsYou: null, sorted.Count == 0 ? "no instances match" : null), titles);
    }

    /// <summary>The response with a list row per instance, each built by <see cref="InstanceSummaries.Of"/>.</summary>
    private ListInstancesResponse Rows(ListInstancesResponse listed, ItemTitles titles)
    {
        var now = clock.GetUtcNow();
        return listed with { Rows = [.. listed.Instances.Select(v => InstanceSummaries.Of(v, new InstanceSummaryContext(now, ItemTitle: titles.For(v))))] };
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
