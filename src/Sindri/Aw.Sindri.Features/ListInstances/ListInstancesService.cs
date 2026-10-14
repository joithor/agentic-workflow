using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>
/// Finds the instances by their search attributes (engine spec §5.1) and asks each for its view, so every line is a
/// full status header. '--needs-me' is served here by a visibility query on 'NeedsYou = true', shaped like API's
/// 'GET /needs-you' (Decisions, then Tasks; README X5) until API P4 builds that route, which it then reads instead.
/// </summary>
/// <param name="directory">Resolves an address or a bare item ('address').</param>
/// <param name="instances">The visibility query.</param>
/// <param name="runtime">Each instance's 'GetInstance' Query; one that ended between the list and its query (SND-WF-001) is left out.</param>
internal sealed partial class ListInstancesService(IInstanceDirectory directory, IWorkflowInstances instances, IInstanceRuntime runtime) : IService<ListInstancesRequest, ListInstancesResponse>
{
    public async ValueTask<Result<ListInstancesResponse>> Handle(ListInstancesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Address is { } reference)
        {
            return await One(reference, cancellationToken).ConfigureAwait(false);
        }

        if (request.State is { } state && !StateName.IsMatch(state))
        {
            return SindriErrors.InvalidInput.With($"'{state}' isn't a state name (fix: --state <name>, e.g. --state produce or --state parked)");
        }

        var rows = await instances.List(Query(request), ended: request.Item is not null || request.State is not null, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var views = new List<InstanceView>();
        foreach (var row in rows.Value)
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

        return Listed(request.NeedsMe, views);
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
        return view.IsError ? view.Error : new ListInstancesResponse([view.Value], NeedsYou: null, Empty: null);
    }

    private static ListInstancesResponse Listed(bool needsMe, List<InstanceView> views)
    {
        if (needsMe)
        {
            var sections = NeedsYouList.Of(views);
            return new([.. sections.Decisions, .. sections.Tasks], sections, sections.IsEmpty ? NeedsYouList.Nothing : null);
        }

        var sorted = views.OrderBy(static v => v.Address.Value, StringComparer.Ordinal).ToList();
        return new(sorted, NeedsYou: null, sorted.Count == 0 ? "no instances match" : null);
    }

    /// <summary>The visibility query: each filter given, ANDed.</summary>
    private static string? Query(ListInstancesRequest request)
    {
        var parts = new List<string>(4);
        if (request.NeedsMe)
        {
            parts.Add("NeedsYou = true");
        }

        if (request.Item is { } item)
        {
            parts.Add($"Item = '{item.Value}'");
        }

        if (request.State is { } state)
        {
            parts.Add($"State = '{state}'");
        }

        if (request.Query is { Length: > 0 } query)
        {
            parts.Add($"({query})");
        }

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StateName { get; }
}
