using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 5: start the stack and wait until Onyx answers with the pinned version. Whether it is done is a live check, not only a stamp.
internal sealed class OnyxStartStep(IContainerRuntime runtime, IOnyxHealth health, OnyxReadiness readiness, IOnyxProgress progress, TimeProvider clock) : IOnyxUpStep
{
    /// <summary>The least time between two `compose up` attempts while a dependency is still starting.</summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(20);

    public string Name => OnyxSteps.Start;

    public string? After => OnyxSteps.Pull;

    public string Stamp(OnyxUpContext context) => OnyxSteps.Stamp(context.Done.GetValueOrDefault(OnyxSteps.Pull, ""), "start");

    public async ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
    {
        if (await IsDone(context, recorded, cancellationToken).ConfigureAwait(false))
        {
            return OnyxStepOutcome.Skipped;
        }

        progress.Line($"{Name}: running");
        var ran = await Run(context, cancellationToken).ConfigureAwait(false);
        return ran.IsError ? ran.Error : OnyxStepOutcome.Ran;
    }

    private async ValueTask<bool> IsDone(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
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

    private async ValueTask<Result<Unit>> Run(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var begun = clock.GetUtcNow();
        var started = runtime.Up(context.Project, _ => progress.Tick(), cancellationToken);
        if (started.IsError)
        {
            if (!DependencyStillStarting(started.Error))
            {
                return started.Error;
            }

            // api_server restarts once on a cold start (Postgres isn't accepting yet), so compose refuses to create nginx; ask again until it does.
            progress.Line("Onyx's api_server is still starting (it restarts once while Postgres comes up); waiting");
            var retried = await RetryUp(context, begun, cancellationToken).ConfigureAwait(false);
            if (retried.IsError)
            {
                return retried.Error;
            }
        }

        var ready = await readiness.Wait(context, progress, cancellationToken, OnyxReadiness.Deadline - (clock.GetUtcNow() - begun)).ConfigureAwait(false);
        if (ready.IsError)
        {
            return ready.Error;
        }

        context.Version = ready.Value;
        return Results.Ok();
    }

    private async ValueTask<Result<Unit>> RetryUp(OnyxUpContext context, DateTimeOffset begun, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (clock.GetUtcNow() - begun >= OnyxReadiness.Deadline)
            {
                return readiness.TimedOut(context, cancellationToken);
            }

            await Task.Delay(RetryInterval, clock, cancellationToken).ConfigureAwait(false);
            progress.Tick();
            var containers = runtime.Ps(context.Project, cancellationToken);
            if (!containers.IsError && Running(containers.Value, "nginx"))
            {
                return Results.Ok();
            }

            var again = runtime.Up(context.Project, _ => progress.Tick(), cancellationToken);
            if (!again.IsError)
            {
                return Results.Ok();
            }

            if (!DependencyStillStarting(again.Error))
            {
                return again.Error;
            }
        }
    }

    // Compose words it "dependency failed to start: container X is unhealthy" (older and newer releases differ), and it is only this one that waiting fixes.
    private static bool DependencyStillStarting(Error error)
        => error.Code == SindriErrors.OnyxComposeFailed.Code
            && (error.Message.Contains("is unhealthy", StringComparison.OrdinalIgnoreCase)
                || error.Message.Contains("dependency failed to start", StringComparison.OrdinalIgnoreCase)
                || error.Message.Contains("failed to start: container", StringComparison.OrdinalIgnoreCase));

    private static bool Running(IReadOnlyList<ComposeContainer> containers, string service)
        => containers.Any(c => c.Service == service && c.State == "running");
}
