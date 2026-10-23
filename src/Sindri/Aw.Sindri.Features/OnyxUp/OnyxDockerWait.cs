using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>
/// Waits, before the preflight, for a Docker that is starting or not running yet (<see cref="ContainerRuntimeFacts.DaemonMayStillStart"/>): a line naming the runtime
/// and what to do meanwhile, then a dot per inspection, every <see cref="Interval"/> up to <see cref="Window"/>. The preflight then judges the last answer as usual,
/// so a Docker that never comes up is SND-ONYX-001 naming the runtime and its fix. Only `onyx up` (the human's verb, and the app's setup job) waits; `onyx status` doesn't.
/// </summary>
/// <param name="facts">The scope's facts: the wait leaves its last answer there for the preflight.</param>
/// <param name="progress">Where the line and the dots go.</param>
/// <param name="clock">The clock the wait is measured and paced by.</param>
internal sealed class OnyxDockerWait(OnyxFacts facts, IOnyxProgress progress, TimeProvider clock)
{
    /// <summary>How long `onyx up` waits for Docker to answer: long enough for Docker Desktop or OrbStack to start from cold or wake up.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(3);

    /// <summary>The gap between two inspections.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>Waits until Docker answers (or answers with a problem a wait can't fix), or the window runs out. A cancelled token ends it with OperationCanceledException.</summary>
    /// <param name="cancellationToken">Cancels the wait (Ctrl-C).</param>
    /// <returns>A task that completes when the wait is over.</returns>
    internal async ValueTask Wait(CancellationToken cancellationToken)
    {
        var docker = facts.Inspect(cancellationToken);
        if (!docker.DaemonMayStillStart)
        {
            return;
        }

        progress.Line(DockerDaemonAdvice.WaitLine(docker, Window));
        var begun = clock.GetUtcNow();
        while (clock.GetUtcNow() - begun < Window)
        {
            await Task.Delay(Interval, clock, cancellationToken).ConfigureAwait(false);
            docker = facts.Reinspect(cancellationToken);
            progress.Tick();
            if (!docker.DaemonMayStillStart)
            {
                progress.Line($"{DockerDaemonAdvice.Name(docker.Kind)} answered");
                return;
            }
        }

        facts.NoteWait(clock.GetUtcNow() - begun);
    }
}
