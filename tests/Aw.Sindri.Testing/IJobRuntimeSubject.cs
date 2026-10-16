using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>A job runtime under test (API P6 plan 1.9): the chained start of <see cref="EchoJobRequest"/>, the runner behind it and the ledger they write.</summary>
public interface IJobRuntimeSubject : IAsyncDisposable
{
    /// <summary>What the echo work saw.</summary>
    public EchoJobProbe Probe { get; }

    /// <summary>The job events and anything else the runtime appended, in order.</summary>
    public IReadOnlyList<LedgerEvent> Events { get; }

    /// <summary>Heavy-job slots the runtime holds now.</summary>
    public int HeavySlotsHeld { get; }

    /// <summary>Starts an echo job through the chained start as <paramref name="caller"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="caller">The starting caller.</param>
    /// <returns>The answer.</returns>
    public Task<Result<JobStartedResponse>> Start(EchoJobRequest request, CallerContext caller);

    /// <summary>Cancels a running job as CancelJob does: appends 'job.cancelled' and trips the work token.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The answer.</returns>
    public Task<Result<CommandAccepted>> Cancel(JobId job);

    /// <summary>Waits until every started job has ended and its last event is appended.</summary>
    /// <returns>A task that completes then.</returns>
    public Task Quiesce();

    /// <summary>Runs another command through the same gate and says whether it got through: a gate held by a job never lets it, and a hang guard on the real clock turns that into false.</summary>
    /// <returns>True when the gate was free.</returns>
    public Task<bool> CommandGateIsFree();
}
