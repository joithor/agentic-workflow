using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The scripted job's work.</summary>
/// <param name="jobs">The running job's context.</param>
public sealed class ProbeJobService(IJobContextAccessor jobs) : IService<ProbeJobRequest, ProbeReport>
{
    public async ValueTask<Result<ProbeReport>> Handle(ProbeJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Mode)
        {
            case "fail":
                return SindriErrors.ServiceFailed.With("it broke");
            case "throw":
                throw new InvalidOperationException("kaboom");
            case "big":
                return new ProbeReport(new string('x', 20_000));
            case "secret":
                jobs.Current?.Progress.Report("token ghp_" + new string('a', 36));
                return new ProbeReport("ok");
            case "job":
                await Task.Yield();
                return new ProbeReport(jobs.Current?.Job.Value ?? "none");
            default:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return new ProbeReport("unreachable");
        }
    }
}
