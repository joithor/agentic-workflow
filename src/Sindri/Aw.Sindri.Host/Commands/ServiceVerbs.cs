using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RunService;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri services', 'services run', 'pause' and 'resume' (API P6 unit H): clients of the service routes. Human verbs start the control plane unless
/// --no-start; inside an agent session they never do (API spec §7). 'run' follows its job (see <see cref="JobFollower"/>) unless --detach.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a run starts.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ServiceVerbs(ControlPlaneClient client, Output output, JobFollower follower, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        var parsed = ServiceCommandLine.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        Error? down;
        try
        {
            down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, interrupt).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (interrupt.IsCancellationRequested)
        {
            return await output.Error(SindriErrors.NotRunning.With("interrupted before the job started (fix: run the command again)")).ConfigureAwait(false);   // Ctrl-C while the control plane was starting
        }

        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            "run" => await StartRun(command, interrupt, again).ConfigureAwait(false),
            "pause" => await Toggle(command, "pause", interrupt).ConfigureAwait(false),
            "resume" => await Toggle(command, "resume", interrupt).ConfigureAwait(false),
            _ => await List(command, interrupt).ConfigureAwait(false),
        };
    }

    private async Task<int> List(ServiceCommandLine command, CancellationToken ct)
    {
        var answer = await client.GetView<ListServicesResponse>("/sindri/services", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.AsOf.Rebuilding || answer.Value.Value.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(ServiceText.Render(answer.Value.Value), code).ConfigureAwait(false);
    }

    private async Task<int> StartRun(ServiceCommandLine command, CancellationToken interrupt, CancellationToken again)
    {
        var route = $"/sindri/services/{Uri.EscapeDataString(command.Name ?? string.Empty)}/run";   // Ctrl-C ends the POST (nothing else bounds a hung start); the job may already exist on the server, which `sindri jobs` shows
        var started = await client.Post<RunServiceRequest, JobStartedResponse>(route, body: null, interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (!command.Detach)
        {
            return await follower.Follow(started.Value.Value, command.Json, interrupt, again).ConfigureAwait(false);
        }

        return command.Json
            ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Toggle(ServiceCommandLine command, string verb, CancellationToken ct)
    {
        var route = $"/sindri/services/{Uri.EscapeDataString(command.Name ?? string.Empty)}/{verb}";
        var accepted = verb == "pause"
            ? await client.Post<PauseServiceRequest, CommandAccepted>(route, body: null, ct).ConfigureAwait(false)
            : await client.Post<ResumeServiceRequest, CommandAccepted>(route, body: null, ct).ConfigureAwait(false);
        return accepted.IsError
            ? await output.Error(accepted.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{command.Name} {(verb == "pause" ? "paused" : "resumed")} (sequence {accepted.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }
}
