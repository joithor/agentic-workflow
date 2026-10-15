using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>
/// The approved profile's services (name and cadence, plan G5), each joined with its row in the 'services' view: the
/// last run, and the next slot (the last slot plus the cadence; null before the first run). Paused is false until P6.
/// A service the approval dropped leaves the list, though its row stays in the view.
/// </summary>
internal sealed class ListServicesService(IApprovedProfile approvals, IViewReader views, IProjectorStatus status)
    : IService<ListServicesRequest, ListServicesResponse>
{
    public async ValueTask<Result<ListServicesResponse>> Handle(ListServicesRequest request, CancellationToken cancellationToken)
    {
        var current = status.Current;
        var asOf = current.AsOf;
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var rows = await views.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var runs = ApprovedServices.Of(rows.Value, approved.Value).ToDictionary(r => r.Name.Value, StringComparer.Ordinal);
        IReadOnlyList<ServiceView> services = [.. (approved.Value?.Profile.Services ?? SindriProfile.NoServices)
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => View(s.Key, s.Value.Every, runs.GetValueOrDefault(s.Key)))];
        return new ListServicesResponse(services, EmptyText.For(current, services.Count == 0, EmptyText.Services), asOf);
    }

    /// <summary>One service; the approved profile was checked, so its cadence parses (an unparseable one shows no next slot).</summary>
    private static ServiceView View(string name, string every, ServiceRow? row)
    {
        var cadence = Cadence.Parse(every);
        DateTimeOffset? next = row?.LastRun is { } last && cadence.IsSuccess ? last.Slot + cadence.Value.Interval : null;
        return new ServiceView(ServiceName.From(name), every, Paused: false, row?.LastRun, next);
    }
}
