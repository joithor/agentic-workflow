using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// A background service the scheduler runs on the profile's cadence (factory addendum §17.29.1). S1 ships the heartbeat;
/// the index, intake and scorer join later as blocks.
/// </summary>
public interface IScheduledService
{
    /// <summary>The name the profile's services map uses.</summary>
    /// <returns>The service name.</returns>
    public ServiceName Identity();

    /// <summary>Runs once.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the run (shutdown).</param>
    /// <returns>How the run ended (<see cref="ScheduledResult.Worked"/> unless it truly did nothing), or the error the scheduler records as a failed run.</returns>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken);
}
