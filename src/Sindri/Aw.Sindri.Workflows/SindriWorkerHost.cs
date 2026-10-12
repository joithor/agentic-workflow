using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="ITemporalWorker"/> over the SDK's TemporalWorker (engine spec rev 8 §0). Engine P0 spike.</summary>
/// <param name="steps">The RunStep activities.</param>
/// <param name="events">The RecordEvent activity.</param>
internal sealed class SindriWorkerHost(RunStepActivities steps, RecordEventActivities events) : ITemporalWorker
{
    public Task Run(TemporalEndpoint endpoint, CancellationToken cancellationToken) =>
        steps is null || events is null ? Task.CompletedTask : throw new NotSupportedException("red");
}
