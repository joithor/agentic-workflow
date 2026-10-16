using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>ListJobs (API P6 unit E): scoped like P3 and P4 (a session sees its repo's and repo-less jobs). Red until then.</summary>
internal sealed class ListJobsService : IService<ListJobsRequest, JobList>
{
    public ListJobsService(IJobViews jobs, IProjectorStatus status, ICallerContextAccessor callers)
    {
        _ = (jobs, status, callers);
    }

    public ValueTask<Result<JobList>> Handle(ListJobsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit E");
}
