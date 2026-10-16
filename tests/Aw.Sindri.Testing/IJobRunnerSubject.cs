using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IJobRunner"/> under test (API P6 review T8): the runner, the echo work it runs and what it recorded.</summary>
public interface IJobRunnerSubject : IAsyncDisposable
{
    /// <summary>The runner.</summary>
    public IJobRunner Runner { get; }

    /// <summary>What the echo work saw.</summary>
    public EchoJobProbe Probe { get; }

    /// <summary>Hands the runner an accepted start of an echo job. The inline fake returns when the work ended, the real runner as soon as it is under way.</summary>
    /// <param name="job">The job.</param>
    /// <param name="request">The echo request.</param>
    /// <param name="caller">The starting caller.</param>
    /// <param name="service">The service the job runs, or null.</param>
    /// <returns>A task that completes when the runner's Start returned.</returns>
    public Task Run(JobId job, EchoJobRequest request, CallerContext caller, ServiceName? service);

    /// <summary>Waits until every started job has ended and what it records is there.</summary>
    /// <returns>A task that completes then.</returns>
    public Task Settle();

    /// <summary>The progress lines the work reported for <paramref name="job"/>, in order.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The lines.</returns>
    public IReadOnlyList<string> Lines(JobId job);

    /// <summary>How <paramref name="job"/> ended, or null when no result was recorded (it was cancelled).</summary>
    /// <param name="job">The job.</param>
    /// <returns>The end, or null.</returns>
    public JobEnd? End(JobId job);
}
