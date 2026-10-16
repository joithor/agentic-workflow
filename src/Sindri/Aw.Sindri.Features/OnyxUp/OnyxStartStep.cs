using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 5: start the stack and wait until Onyx answers with the pinned version. Whether it is done is a live check, not only a stamp.
internal sealed class OnyxStartStep(IContainerRuntime runtime, IOnyxHealth health, OnyxReadiness readiness, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Start;

    public string Stamp(OnyxUpContext context) => OnyxSteps.Stamp(context.Done.GetValueOrDefault(OnyxSteps.Pull, ""), "start");

    public async ValueTask<bool> IsDone(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
    {
        if (!string.Equals(recorded, Stamp(context), StringComparison.Ordinal))
        {
            return false;
        }

        var containers = runtime.Ps(context.Project, cancellationToken);
        if (containers.IsError || !Running(containers.Value, "nginx") || !Running(containers.Value, "api_server"))
        {
            return false;
        }

        var probe = await health.Probe(context.Port, cancellationToken).ConfigureAwait(false);
        context.Version = probe.BackendVersion;
        return probe.Healthy && probe.BackendVersion is { } version && OnyxReadiness.SameVersion(version, context.Pins.Version);
    }

    public async ValueTask<Result<Unit>> Run(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var started = runtime.Up(context.Project, _ => progress.Tick(), cancellationToken);
        if (started.IsError)
        {
            return started.Error;
        }

        var ready = await readiness.Wait(context, progress, cancellationToken).ConfigureAwait(false);
        if (ready.IsError)
        {
            return ready.Error;
        }

        context.Version = ready.Value;
        return Results.Ok();
    }

    private static bool Running(IReadOnlyList<ComposeContainer> containers, string service)
        => containers.Any(c => c.Service == service && c.State == "running");
}
