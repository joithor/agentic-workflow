using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetJob;

/// <summary>Reads a job (API P6 unit E): scoped like P3 and P4 (a session sees its repo's and repo-less jobs). Red until then.</summary>
internal sealed class GetJobService : IService<GetJobRequest, JobView>
{
    public ValueTask<Result<JobView>> Handle(GetJobRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit E");
}
