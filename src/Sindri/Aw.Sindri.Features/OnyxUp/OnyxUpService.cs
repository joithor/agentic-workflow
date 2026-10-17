using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>
/// Spec §4.1 steps 1-5: preflight, then fetch, config, pull and start, each recorded in state.json with the stamp of its inputs only after it succeeds, so a rerun, an
/// interruption or a crash resumes at the first step whose inputs changed or whose result is gone and repeats nothing earlier. One `up` runs at a time (the workspace lock).
/// The port is the preflight's (3000, or the first free fallback), except that while our own nginx publishes a port (ps shows it) that port stays.
/// </summary>
internal sealed class OnyxUpService(IOnyxWorkspace workspace, OnyxFacts facts, IOnyxPinsSource pinsSource, IOnyxProgress progress, IEnumerable<IOnyxUpStep> steps)
    : IService<OnyxUpRequest, OnyxUpResponse>
{
    private readonly IReadOnlyList<IOnyxUpStep> _steps = OnyxUpPlan.Validate(steps);

    public async ValueTask<Result<OnyxUpResponse>> Handle(OnyxUpRequest request, CancellationToken cancellationToken)
    {
        var lockAttempt = workspace.TryLock();
        if (lockAttempt.IsError)
        {
            return lockAttempt.Error;
        }

        using var held = lockAttempt.Value;
        if (held is null)
        {
            return SindriErrors.CommandBusy.With("another `sindri onyx up` is running; wait for it to finish, then run this again");
        }

        return await Run(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<OnyxUpResponse>> Run(CancellationToken cancellationToken)
    {
        var state = workspace.ReadState();
        if (state is null && workspace.StateIsUnreadable())
        {
            progress.Line("state.json is unreadable; starting over from the first step (each step checks what is already there)");
        }

        var pins = pinsSource.Pins;
        var preflight = Preflight(state, pins, cancellationToken);
        if (preflight.IsError)
        {
            return preflight.Error;
        }

        var (port, platform, warnings) = preflight.Value;
        var context = new OnyxUpContext(pins, facts.Paths, port, platform) { Project = facts.ProjectOf(state) };
        var recorded = new Dictionary<string, string>(state?.Steps ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        List<OnyxStepResult> results = [];
        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await RunStep(step, context, recorded, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            results.Add(new OnyxStepResult(step.Name, outcome.Value));
        }

        return new OnyxUpResponse(port, string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), context.Version ?? pins.Version, warnings, results);
    }

    // The step makes its result hold; its stamp is recorded once it does.
    private async ValueTask<Result<OnyxStepOutcome>> RunStep(IOnyxUpStep step, OnyxUpContext context, Dictionary<string, string> recorded, CancellationToken cancellationToken)
    {
        var stamp = step.Stamp(context);
        _ = recorded.TryGetValue(step.Name, out var before);
        var outcome = await step.Execute(context, before, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        if (outcome.Value == OnyxStepOutcome.Skipped)
        {
            progress.Line($"{step.Name}: already done");
        }

        context.Done[step.Name] = stamp;
        if (!string.Equals(before, stamp, StringComparison.Ordinal))
        {
            recorded[step.Name] = stamp;
            workspace.WriteState(new OnyxSetupState(context.Port, new Dictionary<string, string>(recorded, StringComparer.Ordinal), context.Project.Name));
        }

        return outcome;
    }

    // Step 1. The port row accepts the port our own running nginx holds; a failed row stops everything before anything is fetched or written.
    private Result<(int Port, string Platform, IReadOnlyList<string> Warnings)> Preflight(OnyxSetupState? state, OnyxPins pins, CancellationToken cancellationToken)
    {
        var (docker, host) = facts.Read(state?.Port, cancellationToken);
        var report = OnyxPreflight.Evaluate(docker, host, pins, OwnPort(state, docker, cancellationToken));
        if (report.Blocks)
        {
            return report.Checks.First(static c => c.State == CheckState.Fail).Problem!;   // why: a failed check always carries its error
        }

        var warnings = report.Checks.Where(static c => c.State == CheckState.Warn).Select(static c => $"{c.Name}: {c.Detail}").ToList();
        warnings.ForEach(progress.Line);
        var platform = facts.Platform(cancellationToken);
        return platform.IsError ? platform.Error : (report.Port, platform.Value, warnings);
    }

    // The port our own running nginx publishes, whatever state.json says: a start that failed or timed out leaves it up, and a lost state file doesn't make it someone else's.
    private int? OwnPort(OnyxSetupState? state, ContainerRuntimeFacts docker, CancellationToken cancellationToken)
    {
        if (!docker.DaemonAnswers || workspace.Read(facts.Paths.Compose) is null)
        {
            return null;   // no compose file yet means no project to ask about
        }

        var containers = facts.Containers(state, cancellationToken);
        return containers.IsSuccess
            ? containers.Value.Where(static c => c.Service == "nginx" && ComposeContainer.IsLive(c.State)).SelectMany(static c => c.Publishers).FirstOrDefault(static p => p.Address == "127.0.0.1")?.PublishedPort
            : null;
    }
}
