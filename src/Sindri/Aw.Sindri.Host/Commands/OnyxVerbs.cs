using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri onyx up' and 'sindri onyx down': they run Docker and write Onyx's files under the Sindri home, so only a human at a terminal runs them (an agent
/// session or a non-terminal gets SND-AUTH-002, exit 1, like 'mcp register --apply'). Local: they run in this process, need no control plane, and change
/// nothing for a refused caller. Progress is on standard error (lines and dots, through IOnyxProgress); standard output carries only the summary.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">Who runs the CLI.</param>
/// <param name="approve">Runs `profile approve [hash]` the way the CLI does (preview without a hash, the typed challenge with one), for the approval `onyx up` shows inline; null skips the offer.</param>
internal sealed class OnyxVerbs(IServiceProvider services, Output output, AgentSession session, Func<string?, CancellationToken, Task<int>>? approve = null)
{
    private const string ConnectorChecklist = "Connect your sources in Onyx (OAuth is yours, and so is the company's sign-off):\n  Linear: the product's teams, with a start date that covers the backtests\n  Slack: the product's channels\nThen `sindri doctor` shows context:onyx and onyx:stack.\n";

    internal async Task<int> Up(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("up", "runs Docker and writes Onyx's files").ConfigureAwait(false);
        }

        Result<OnyxUpResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The runner killed the docker child, the services released the lock and removed their temporary files on the way out; each step is recorded only once done.
            return Interrupted("interrupted; run `sindri onyx up` again to resume");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        var up = answer.Value;
        var account = await Account(up, ct).ConfigureAwait(false);
        if (account.IsError)
        {
            return account.Error.Code is "interrupted" ? Interrupted("interrupted; run `sindri onyx up` again to resume") : await Fail(output, account.Error).ConfigureAwait(false);
        }

        var tail = account.Value is { } done ? await HandOverText(done, ct).ConfigureAwait(false) : null;
        var next = up.NeedsHuman is { Count: > 0 } waiting
            ? $"needs you: {string.Join(", ", waiting)}"
            : tail?.Text ?? "Onyx is running. Account setup (sign-up and Sindri's key) needs a terminal: run `sindri onyx up` in one.";
        var code = tail?.ExitCode ?? 0;
        return json
            ? await output.Json(new OnyxUpSummary(up, account.Value), code).ConfigureAwait(false)
            : await output.Text($"{"url",-10}{up.Url}\n{"version",-10}{up.Version}\n{"ran",-10}{Steps(up.Ran)}\n{"skipped",-10}{Steps(up.Skipped)}\n{next}\n", code).ConfigureAwait(false);
    }

    /// <summary>'sindri onyx open': the sign-up page while no account was made through `onyx up`, else the connectors page.</summary>
    /// <param name="ct">Cancels the verb.</param>
    /// <returns>0 opened (or the address printed), 1 refused or not set up.</returns>
    internal async Task<int> Open(CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("open", "opens a page in the browser").ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        var state = services.GetRequiredService<IOnyxWorkspace>().ReadState();
        if (state is null)
        {
            _ = await output.Error(SindriErrors.InvalidInput.With("Onyx isn't set up on this box (fix: run `sindri onyx up`)")).ConfigureAwait(false);
            return 1;
        }

        var page = new Uri($"http://127.0.0.1:{state.Port}" + (state.Steps.ContainsKey(OnyxSteps.Signup) ? "/admin/add-connector" : "/auth/signup"));
        var opened = services.GetRequiredService<IBrowser>().Open(page);
        return await output.Text(opened ? $"Opening {page}\n" : $"Open {page} in your browser.\n", 0).ConfigureAwait(false);
    }

    private async Task<Result<OnyxAccountResponse?>> Account(OnyxUpResponse up, CancellationToken ct)
    {
        Result<OnyxAccountResponse> account;
        try
        {
            account = await services.GetRequiredService<IService<OnyxAccountRequest, OnyxAccountResponse>>().Handle(new OnyxAccountRequest(up.Port, up.Version), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new Error("interrupted", ErrorKind.Timeout, "interrupted");
        }

        return account.IsError ? account.Error : Results.Success<OnyxAccountResponse?>(account.Value);
    }

    // After the account stage: the profile approval shown inline, the connectors page opened, the checklist printed.
    private async Task<(string Text, int ExitCode)> HandOverText(OnyxAccountResponse done, CancellationToken ct)
    {
        if (done.NeedsHuman)
        {
            return ($"needs you: {string.Join(", ", done.Steps.Where(static s => s.Outcome == OnyxStepOutcome.NeedsHuman).Select(static s => s.Step))} (run `sindri onyx up` at a terminal)", 0);
        }

        var approved = approve is null || await ApproveInline(ct).ConfigureAwait(false);
        _ = services.GetRequiredService<IBrowser>().Open(new Uri(done.Url + "/admin/add-connector"));
        var key = done.KeyPointer is null ? string.Empty : $"{"key",-10}{done.KeyPointer} (a pointer; the key itself is in the store)\n";
        var profile = approved ? string.Empty : "The profile section is written but not approved yet: run `sindri profile approve`.\n";
        return ($"{key}{profile}{ConnectorChecklist}".TrimEnd('\n'), approved ? 0 : 1);
    }

    private async Task<bool> ApproveInline(CancellationToken ct)
    {
        var loaded = await services.GetRequiredService<IProfileSource>().Load(ct).ConfigureAwait(false);
        if (loaded.IsError)
        {
            _ = await output.Error(loaded.Error).ConfigureAwait(false);
            return false;
        }

        if (await approve!(null, ct).ConfigureAwait(false) == 0)
        {
            return true;   // the preview says it is approved already
        }

        return await approve(loaded.Value.Hash.Value, ct).ConfigureAwait(false) == 0;   // the typed challenge, as `sindri profile approve <hash>` asks it
    }

    internal async Task<int> Down(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("down", "stops Docker containers").ConfigureAwait(false);
        }

        Result<OnyxDownResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxDownRequest, OnyxDownResponse>>().Handle(new OnyxDownRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted; run `sindri onyx down` again to finish stopping Onyx");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        var progress = services.GetRequiredService<IOnyxProgress>();
        foreach (var warning in answer.Value.Warnings)
        {
            progress.Line(warning);
        }

        return json
            ? await output.Json(answer.Value, 0).ConfigureAwait(false)
            : await output.Text(answer.Value.WasRunning
                ? $"Onyx stopped: {string.Join(", ", answer.Value.Stopped)} (its data and images are kept; `sindri onyx up` starts it again).\n"
                : "Onyx is stopped (nothing was running).\n", 0).ConfigureAwait(false);
    }

    /// <summary>Prints an error the way the onyx verbs exit: 1 for an SND-ONYX error or a busy command (attention: the message names the fix), 2 for anything else.</summary>
    /// <param name="output">Where the error goes.</param>
    /// <param name="error">The error.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Fail(Output output, Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return error.Code.StartsWith("SND-ONYX-", StringComparison.Ordinal) || error.Code == SindriErrors.CommandBusy.Code ? 1 : 2;
    }

    private static string Steps(IReadOnlyList<string> steps) => steps.Count == 0 ? "none" : string.Join(", ", steps);

    // Through the progress writer, which ends an open row of dots before the line, so the resume line never starts mid-row.
    private int Interrupted(string text)
    {
        services.GetRequiredService<IOnyxProgress>().Line(text);
        return Output.InterruptedExit;
    }

    // The printed refusal: SND-AUTH-002, exit 1 (attention, not a tool error), with nothing called.
    private async Task<int> Refused(string verb, string what)
    {
        _ = await output.Error(SindriErrors.AuthRefused.With(
            $"`sindri onyx {verb}` {what}, so only a human at a terminal runs it (this CLI ran as a session: {session.SessionReason}; fix: ask the human to run `sindri onyx {verb}`)")).ConfigureAwait(false);
        return 1;
    }
}
