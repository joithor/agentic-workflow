using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing.Jobs;

/// <summary>The echo job's work.</summary>
/// <param name="jobs">The running job's context, where the lines go.</param>
/// <param name="callers">The caller the work runs under.</param>
/// <param name="probe">Records what the work saw.</param>
/// <param name="time">Times the wait after each line; a test host gives it a FakeTimeProvider, so a job is "still running" until the test advances the clock.</param>
public sealed class EchoJobService(IJobContextAccessor jobs, ICallerContextAccessor callers, EchoJobProbe probe, TimeProvider time) : IService<EchoJobRequest, EchoReport>
{
    public async ValueTask<Result<EchoReport>> Handle(EchoJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        probe.Started(callers.Current?.Actor, jobs.Current?.Job);
        for (var n = 1; n <= request.Lines; n++)
        {
            jobs.Current?.Progress.Report(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"echo {n}"));
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(request.DelayMs), time, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                probe.SawCancellation();
                throw;
            }
        }

        return request.Throws ? throw new InvalidOperationException("echo threw") : new EchoReport(request.Lines);
    }
}
