using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxEnsure;

/// <summary>
/// Spec §4.2 for `serve`: when the approved profile says Sindri manages Onyx and `onyx up` finished here, make sure the stack runs: the preflight's Docker check (no disk check), the files that are already there, `compose up -d` and a wait
/// for Onyx to answer (up to 5 minutes; serve never blocks on it). It never fetches, pulls, writes a config file or bootstraps; a box where `onyx up` never completed gets the needs-you row instead. What it found is kept in
/// `ensure.txt` (`running`, `needs-setup`, `failed: why`) for the needs-you list and doctor.
/// </summary>
internal sealed class OnyxEnsureService(IManagedOnyx managed, IOnyxWorkspace workspace, OnyxFacts facts, IContainerRuntime runtime, IOnyxHealth health, TimeProvider clock)
    : IService<OnyxEnsureRequest, OnyxEnsureResponse>
{
    private static readonly TimeSpan _wait = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(3);

    public async ValueTask<Result<OnyxEnsureResponse>> Handle(OnyxEnsureRequest request, CancellationToken cancellationToken)
    {
        var isManaged = await managed.Is(cancellationToken).ConfigureAwait(false);
        if (isManaged is not { IsSuccess: true, Value: true })
        {
            return new OnyxEnsureResponse(Endpoint: null, Detail: null);   // an unmanaged Onyx is never touched
        }

        var state = workspace.ReadState();
        var project = facts.Project.Name;
        if (state is null || !state.Steps.ContainsKey(OnyxSteps.Start))
        {
            return Note(project, state?.Port, "needs-setup", "Onyx isn't set up on this box: run `sindri onyx up`");
        }

        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{state.Port}");
        var failure = await Bring(state, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();   // serve stopping mid-start is an interruption, not a failure to note
        return failure is null ? Note(project, state.Port, "running", detail: null) : Note(project, state.Port, "failed", failure, url);
    }

    // Null when Onyx answers; otherwise why not, without a secret.
    private async ValueTask<string?> Bring(OnyxSetupState state, CancellationToken cancellationToken)
    {
        if (facts.DockerProblem(cancellationToken) is { } problem)
        {
            return problem.Message;
        }

        var picked = facts.ProjectInUse(workspace);
        if (picked.IsError)
        {
            return picked.Error.Message;
        }

        if (picked.Value.Missing.Count > 0)
        {
            return $"Onyx's files are gone ({string.Join(", ", picked.Value.Missing)}); run `sindri onyx up`";
        }

        var listed = facts.Containers(picked.Value.Project, cancellationToken);
        if (listed.IsSuccess && Serving(listed.Value))
        {
            return await Answers(state.Port, cancellationToken).ConfigureAwait(false);   // already up: nothing to start
        }

        var started = runtime.Up(picked.Value.Project, static _ => { }, _wait, cancellationToken);
        return started.IsError ? started.Error.Message : await Answers(state.Port, cancellationToken).ConfigureAwait(false);
    }

    private static bool Serving(IReadOnlyList<ComposeContainer> containers)
        => containers.Any(static c => c.Service == "nginx" && c.State == "running") && containers.Any(static c => c.Service == "api_server" && c.State == "running");

    private async ValueTask<string?> Answers(int port, CancellationToken cancellationToken)
    {
        var begun = clock.GetUtcNow();
        while (true)
        {
            var probe = await health.Probe(port, cancellationToken).ConfigureAwait(false);
            if (probe.Healthy)
            {
                return null;
            }

            if (clock.GetUtcNow() - begun >= _wait)
            {
                return "Onyx didn't answer within 5 minutes of the start; run `sindri onyx status`";
            }

            await Task.Delay(_poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private Result<OnyxEnsureResponse> Note(string project, int? port, string state, string? detail, string? url = null)
    {
        _ = workspace.WriteIfChanged(facts.Paths.Ensure, state == "failed" ? $"failed: {detail}\n" : state + "\n", secret: false);
        var address = url ?? (port is { } p ? string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{p}") : string.Empty);
        return new OnyxEnsureResponse(new OnyxEndpoint(project, address, state), detail);
    }
}
