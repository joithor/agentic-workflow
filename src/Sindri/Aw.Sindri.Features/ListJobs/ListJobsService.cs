using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>Lists jobs (API P6 unit E): session-scoped, filtered by state and kind. Red until then.</summary>
internal sealed class ListJobsService : IService<ListJobsRequest, JobList>
{
    public ValueTask<Result<JobList>> Handle(ListJobsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit E");
}
