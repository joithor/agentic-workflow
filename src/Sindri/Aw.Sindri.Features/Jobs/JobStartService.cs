using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The chained service of a job-class command (API P6, plan Unit C): inside the gate it checks the running cap (SND-JOB-002), the definition's precheck,
/// claims (kind, key) (SND-JOB-409 with 'runningJob'), appends 'job.started' and hands the work to <see cref="IJobRunner"/>. It never runs the work.
/// </summary>
/// <typeparam name="TRequest">The job request.</typeparam>
/// <typeparam name="TResult">What the work returns.</typeparam>
internal sealed class JobStartService<TRequest, TResult> : IService<TRequest, JobStartedResponse>
    where TRequest : IJob<TResult>
{
    public ValueTask<Result<JobStartedResponse>> Handle(TRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit C");
}
