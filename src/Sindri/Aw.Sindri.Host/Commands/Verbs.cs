using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The client verbs. status, stop and profile approve call the running control plane's API; profile validate and doctor
/// run the same handlers in this process, read-only, so they work while it is down.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="probe">Finds the running control plane.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="scope">The scope the local verbs resolve their handlers from.</param>
/// <param name="ensureRunning">Starts the control plane before a human's command, or names why not (API spec §7); null: never start it.</param>
internal sealed class Verbs(ControlPlaneClient client, Core.Control.IControlPlaneProbe probe, Output output, IServiceProvider scope,
    Func<CancellationToken, Task<Kernel.Error?>>? ensureRunning = null)
{
    internal async Task<int> Status(bool json, CancellationToken ct)
    {
        var found = await probe.Check(ct).ConfigureAwait(false);
        var status = found.IsError ? found.Error : await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        if (status.IsError)
        {
            var error = status.Error.Code == SindriErrors.NotRunning.Code ? status.Error with { Message = "not running: " + status.Error.Message } : status.Error;
            return await output.Error(error).ConfigureAwait(false);
        }

        var code = status.Value.Value.Profile.NeedsApproval ? 1 : 0;
        return json
            ? await output.Raw(status.Value.Body, code).ConfigureAwait(false)
            : await output.Text(SummaryText.Render(status.Value.Value, found.Value.Endpoint, showUi: false), code).ConfigureAwait(false);
    }

    internal async Task<int> Stop(CancellationToken ct)
    {
        var stopped = await client.Post<StopProcessRequest, StopProcessResponse>("/sindri/stop", body: null, ct).ConfigureAwait(false);
        return stopped.IsError
            ? await output.Error(stopped.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"stopped (pid {stopped.Value.Value.Pid}, epoch {stopped.Value.Value.Epoch} released)\n"), 0).ConfigureAwait(false);
    }

    internal async Task<int> Validate(bool json, CancellationToken ct)
    {
        var valid = await scope.GetRequiredService<IService<ValidateProfileRequest, ValidateProfileResponse>>().Handle(new ValidateProfileRequest(), ct).ConfigureAwait(false);
        if (valid.IsError)
        {
            return await output.Error(valid.Error).ConfigureAwait(false);
        }

        var state = valid.Value.Approved ? "approved" : "not approved (fix: sindri profile approve)";
        return json
            ? await output.Json(valid.Value, valid.Value.Approved ? 0 : 1).ConfigureAwait(false)
            : await output.Text($"Profile valid: {valid.Value.Source}\nhash {valid.Value.Hash.Value}, {state}\n", valid.Value.Approved ? 0 : 1).ConfigureAwait(false);
    }

    internal async Task<int> Doctor(bool json, CancellationToken ct)
    {
        var report = (await scope.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(new RunDoctorRequest(), ct).ConfigureAwait(false)).Value;
        if (json)
        {
            return await output.Json(report, report.ExitCode).ConfigureAwait(false);
        }

        var lines = report.Checks.Select(c => $"{Word(c.State),-5} {c.Name,-8} {c.Detail}{(c.Fix is null ? string.Empty : $" (fix: {c.Fix})")}");
        return await output.Text(string.Join('\n', lines) + "\n", report.ExitCode).ConfigureAwait(false);
    }

    internal async Task<int> Approve(string? hashText, bool interactive, TextReader input, CancellationToken ct)
    {
        ProfileHash? hash = hashText is not null && ProfileHash.TryFrom(hashText, out var parsed) ? parsed : null;
        var refusal = Precheck(hashText, hash, interactive) ?? await BeforeTheCall(hash, input, ct).ConfigureAwait(false);
        if (refusal is not null)
        {
            return await output.Error(refusal).ConfigureAwait(false);
        }

        var answer = await client.Post<ApproveProfileRequest, ApproveProfileResponse>("/sindri/profile/approve", new ApproveProfileRequest(hash, hash is not null), ct).ConfigureAwait(false);
        return answer.IsError ? await output.Error(answer.Error).ConfigureAwait(false) : await output.Text(ApprovalText(answer.Value.Value, hash is null), answer.Value.Value.Approved ? 0 : 1).ConfigureAwait(false);
    }

    /// <summary>A malformed hash, or a hash without a terminal: refused before any call, and before starting anything.</summary>
    private static Kernel.Error? Precheck(string? hashText, ProfileHash? hash, bool interactive) => (hashText, hash) switch
    {
        ({ } text, null) => SindriErrors.InvalidInput.With($"'{text}' is not a profile hash (64 hex digits)"),
        (_, { }) when !interactive => SindriErrors.ProfileNeedsTerminal.With("approving needs an interactive terminal: run it yourself and type the first 6 characters of the hash"),
        _ => null,
    };

    /// <summary>Starts the control plane if this CLI may (API spec §7), then asks for the hash's first 6 characters.</summary>
    private async Task<Kernel.Error?> BeforeTheCall(ProfileHash? hash, TextReader input, CancellationToken ct)
    {
        var down = ensureRunning is null ? null : await ensureRunning(ct).ConfigureAwait(false);
        return down ?? (hash is { } h ? await Confirmation(h, input).ConfigureAwait(false) : null);
    }

    private async Task<Kernel.Error?> Confirmation(ProfileHash hash, TextReader input)
    {
        var typed = await Prompt(hash, input).ConfigureAwait(false);
        return string.Equals(typed, hash.Abbreviated, StringComparison.Ordinal)
            ? null
            : SindriErrors.ProfileNeedsTerminal.With($"you typed '{typed}', not the hash's first 6 characters; nothing was approved");
    }

    private async Task<string?> Prompt(ProfileHash hash, TextReader input)
    {
        await output.Out.WriteAsync($"Type the first 6 characters of {hash.Value} to approve it: ").ConfigureAwait(false);
        return (await input.ReadLineAsync().ConfigureAwait(false))?.Trim();
    }

    private static string ApprovalText(ApproveProfileResponse a, bool preview) => (preview, a.Approved) switch
    {
        (_, true) when preview => $"Approved already: {a.Hash.Value}\n",
        (true, _) => $"hash {a.Hash.Value}\nchanged since the last approval: {(a.Changes.Count == 0 ? "nothing" : string.Join(", ", a.Changes))}\nto approve: sindri profile approve {a.Hash.Value}\n",
        _ => $"Approved {a.Hash.Value}\n",
    };

    private static string Word(CheckState state) => state switch
    {
        CheckState.Ok => "ok",
        CheckState.Warn => "warn",
        CheckState.Fail => "fail",
        _ => "?",
    };
}
