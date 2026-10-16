using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The real <see cref="JobRunner"/> over the in-memory runtime as a runner subject (API P6 review T8): what it records is read back from the ledger.</summary>
internal sealed class RealJobRunnerSubject(RealJobRuntime runtime) : IJobRunnerSubject
{
    private readonly JobDefinition _definition = runtime.Get<IEnumerable<JobDefinition>>().Single(static d => d.Request == typeof(EchoJobRequest));

    public IJobRunner Runner => runtime.Runner;

    public EchoJobProbe Probe => runtime.Probe;

    public static async ValueTask<RealJobRunnerSubject> Open()
    {
        var runtime = new RealJobRuntime();
        await runtime.Open();
        return new RealJobRunnerSubject(runtime);
    }

    public async Task Run(JobId job, EchoJobRequest request, CallerContext caller, ServiceName? service) =>
        await runtime.Runner.Start(new JobStart(job, JobKind.From("EchoJob"), _definition.KeyOf(request), _definition, request, caller, runtime.H.Held.Current!, service), TestContext.Current.CancellationToken);

    public Task Settle() => runtime.Quiesce();

    public IReadOnlyList<string> Lines(JobId job) =>
        [.. runtime.Events.Where(static e => string.Equals(e.Envelope.Type, EventTypes.JobProgress, StringComparison.Ordinal))
            .Select(e => runtime.H.Events.Payload<JobProgress>(e).Value).Where(p => p.Job == job).Select(static p => p.Line)];

    public JobEnd? End(JobId job) =>
        runtime.Events.Where(static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal))
            .Select(e => runtime.H.Events.Payload<JobFinished>(e).Value).Where(f => f.Job == job)
            .Select(static f => new JobEnd(f.Outcome == JobOutcome.Succeeded, f.Code)).FirstOrDefault();

    public ValueTask DisposeAsync() => runtime.DisposeAsync();
}
