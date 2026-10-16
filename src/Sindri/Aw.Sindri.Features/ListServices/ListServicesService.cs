using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>
/// The approved profile's services (name and cadence, plan G5), each joined with its row in the 'services' view: the
/// last run, and the next slot (the last slot plus the cadence; null before the first run). Paused and the running job come from P6.
/// A service the approval dropped leaves the list, though its row stays in the view.
/// </summary>
internal sealed class ListServicesService(IApprovedProfile approvals, IViewReader views, IProjectorStatus status, IJobViews jobs)
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

        var running = await jobs.Running(cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var runs = ApprovedServices.Of(rows.Value, approved.Value).ToDictionary(r => r.Name.Value, StringComparer.Ordinal);
        IReadOnlyList<ServiceView> services = [.. (approved.Value?.Profile.Services ?? SindriProfile.NoServices)
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => View(s.Key, s.Value.Every, runs.GetValueOrDefault(s.Key), running.Value.FirstOrDefault(j => j.Service?.Value == s.Key)?.Id))];
        return new ListServicesResponse(services, EmptyText.For(current, services.Count == 0, EmptyText.Services), asOf);
    }

    /// <summary>
    /// One service; the approved profile was checked, so its cadence parses (an unparseable one shows no next slot). The next slot is the scheduler's last
    /// slot plus the cadence, so a manual run does not move it, and there is none while the service is paused.
    /// </summary>
    private static ServiceView View(string name, string every, ServiceRow? row, JobId? job)
    {
        var cadence = Cadence.Parse(every);
        var paused = row?.Paused == true;
        DateTimeOffset? next = !paused && row?.ScheduledSlot is { } slot && cadence.IsSuccess ? slot + cadence.Value.Interval : null;
        return new ServiceView(ServiceName.From(name), every, paused, row?.LastRun, next, job);
    }
}
