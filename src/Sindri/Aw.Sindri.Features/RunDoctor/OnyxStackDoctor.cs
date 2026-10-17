using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's `onyx:stack` check: once `sindri onyx up` has run (its state.json exists), the stack is read back from Docker and judged. No row before that. Docker not answering and
/// a stopped stack are warnings with the fix; a failure is, in this order: a container that isn't running or reports unhealthy, a published port that isn't bound to 127.0.0.1
/// (Sindri never writes such a binding, so something changed the stack: stop it and report it), and a container whose image isn't one the lock pins for this platform.
/// </summary>
/// <param name="workspace">Where the setup state lives.</param>
/// <param name="facts">Reads the containers and the Docker platform.</param>
/// <param name="pinsSource">The lock.</param>
internal sealed class OnyxStackDoctor(IOnyxWorkspace workspace, OnyxFacts facts, IOnyxPinsSource pinsSource) : IDoctorChecks
{
    /// <summary>The check's name.</summary>
    internal const string Name = "onyx:stack";

    private const string Loopback = "127.0.0.1";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        => await Check(cancellationToken).ConfigureAwait(false) is { } check ? [check] : [];

    /// <summary>Judges the stack.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The row, or null before `sindri onyx up` has recorded anything.</returns>
    internal ValueTask<DoctorCheck?> Check(CancellationToken cancellationToken) => ValueTask.FromResult(Judge(cancellationToken));

    private DoctorCheck? Judge(CancellationToken cancellationToken)
    {
        if (workspace.ReadState() is not { } state)
        {
            return workspace.StateIsUnreadable() ? new DoctorCheck(Name, CheckState.Warn, "state file unreadable; `sindri onyx up` rebuilds it", "sindri onyx up") : null;
        }

        var containers = facts.Containers(state, cancellationToken);
        var platform = facts.Platform(cancellationToken);
        if (containers.IsError || platform.IsError)
        {
            return Unreadable(containers.IsError ? containers.Error : platform.Error, facts.ProjectOf(state));
        }

        return !containers.Value.Any(static c => ComposeContainer.IsLive(c.State))
            ? new DoctorCheck(Name, CheckState.Warn, "stopped: no Onyx container is running", "sindri onyx up")
            : Failure(state, containers.Value, platform.Value) ?? Ok(containers.Value);
    }

    // Docker not answering (001) and docker answering but a command failing or being cut short (011) are different problems with different fixes.
    private static DoctorCheck Unreadable(Error error, ComposeProject project)
        => error.Code == SindriErrors.OnyxDockerUnavailable.Code
            ? new DoctorCheck(Name, CheckState.Warn, $"docker isn't running ({error.Code}); the Onyx stack can't be read", "start Docker Desktop (or the docker service), then run sindri doctor again")
            : new DoctorCheck(Name, CheckState.Warn, $"docker answered but the Onyx stack couldn't be read ({error.Code}): {error.Message}", $"run `docker compose -p {project.Name} ps` to see why, then run sindri doctor again");

    private DoctorCheck? Failure(OnyxSetupState state, IReadOnlyList<ComposeContainer> containers, string platform)
    {
        if (containers.FirstOrDefault(static c => c.State != "running" || c.Health == "unhealthy") is { } down)
        {
            var crashLooping = down.State == "restarting" ? " (crash-looping)" : "";
            return Fail($"{down.Service} is {down.State}{crashLooping}{(down.Health is null ? "" : $" ({down.Health})")}", $"docker compose -p {facts.ProjectOf(state).Name} logs " + down.Service + ", then sindri onyx up");
        }

        foreach (var container in containers)
        {
            if (container.Publishers.FirstOrDefault(static p => p.Address != Loopback) is { } exposed)
            {
                return Fail($"{container.Service} publishes port {exposed.PublishedPort.ToString(CultureInfo.InvariantCulture)} on {exposed.Address}, not on {Loopback}", "sindri onyx down, then report this: Sindri never writes such a binding");
            }
        }

        var pinned = pinsSource.Pins.Images.Where(i => i.Platform == platform).Select(static i => ImageNames.WithoutDefaults(i.Repository + "@" + i.Digest)).ToHashSet(StringComparer.Ordinal);
        return containers.FirstOrDefault(c => !pinned.Contains(ImageNames.WithoutDefaults(c.Image))) is { } other
            ? Fail($"{other.Service} runs {other.Image}, which isn't an image context/onyx.lock pins for {platform}", "sindri onyx down, remove Sindri's Onyx files under the Sindri home, then sindri onyx up")
            : null;
    }

    private static DoctorCheck Ok(IReadOnlyList<ComposeContainer> containers)
    {
        var port = containers.SelectMany(static c => c.Publishers).FirstOrDefault(static p => p.Address == Loopback)?.PublishedPort;
        return new DoctorCheck(Name, CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"{containers.Count} containers running, healthy, bound to {Loopback}:{port}, digests match the pins"), Fix: null);
    }

    private static DoctorCheck Fail(string detail, string fix) => new(Name, CheckState.Fail, detail, fix);
}
