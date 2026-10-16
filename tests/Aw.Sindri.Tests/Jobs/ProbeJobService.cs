using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The scripted job's work.</summary>
/// <param name="jobs">The running job's context.</param>
/// <param name="latch">What the 'stubborn' mode waits on.</param>
internal sealed class ProbeJobService(IJobContextAccessor jobs, Commands.Chain.FixtureLatch latch) : IService<ProbeJobRequest, ProbeReport>
{
    public async ValueTask<Result<ProbeReport>> Handle(ProbeJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Mode)
        {
            case "job":
                await Task.Yield();
                return new ProbeReport(jobs.Current?.Job.Value ?? "none");
            case "stubborn":   // ignores its token: it returns only when the test releases the latch
                latch.Start();
                await latch.Release.Task.ConfigureAwait(false);
                return new ProbeReport("late");
            case "fail" or "throw" or "big" or "secret" or "repo":
                return Immediate(request.Mode);
            default:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return new ProbeReport("unreachable");
        }
    }

    private Result<ProbeReport> Immediate(string mode)
    {
        switch (mode)
        {
            case "fail":
                return SindriErrors.ServiceFailed.With("it broke");
            case "throw":
                throw new InvalidOperationException("kaboom");
            case "big":
                return new ProbeReport(new string('x', 20_000));
            default:
                jobs.Current?.Progress.Report("token ghp_" + new string('a', 36));
                return new ProbeReport("ok");
        }
    }
}
