using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 6-9. (6) The owner makes their own account in Onyx's sign-up page (Onyx makes the first user the admin; Sindri registers nobody). (7, 8) With that login, asked for once at the terminal and forgotten, Sindri makes its
/// admin key and stores it (<see cref="OnyxKeyMinter"/>). (9) The profile section is written. Each step is recorded in state.json once done, so a rerun skips it. Without a terminal nothing is asked: the stage ends "needs you".
/// </summary>
internal sealed class OnyxAccountService(IOnyxWorkspace workspace, IOnyxPrompts prompts, IBrowser browser, OnyxKeyMinter minter, IOnyxProfileWriter profile, IOnyxProgress progress)
    : IService<OnyxAccountRequest, OnyxAccountResponse>
{
    public async ValueTask<Result<OnyxAccountResponse>> Handle(OnyxAccountRequest request, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{request.Port}");
        var state = workspace.ReadState() ?? new OnyxSetupState(request.Port, new Dictionary<string, string>(StringComparer.Ordinal));
        var steps = new Dictionary<string, string>(state.Steps, StringComparer.Ordinal);
        var pointer = state.KeyPointer;
        List<OnyxStepResult> results = [];

        var signup = await Signup(url, steps, cancellationToken).ConfigureAwait(false);
        results.Add(new OnyxStepResult(OnyxSteps.Signup, signup));
        Save(state, request.Port, steps, pointer);
        if (signup == OnyxStepOutcome.NeedsHuman)
        {
            return new OnyxAccountResponse(url, pointer, ProfileChanged: false, results);
        }

        var key = await Key(request.Port, steps, pointer, cancellationToken).ConfigureAwait(false);
        if (key.IsError)
        {
            return key.Error;
        }

        (pointer, var keyOutcome) = key.Value;
        results.Add(new OnyxStepResult(OnyxSteps.Key, keyOutcome));
        Save(state, request.Port, steps, pointer);
        if (pointer is null)
        {
            return new OnyxAccountResponse(url, KeyPointer: null, ProfileChanged: false, results);   // the key step waits for the human
        }

        var written = profile.SetOnyx(url, pointer, request.Version);
        if (written.IsError)
        {
            return written.Error;
        }

        steps[OnyxSteps.Profile] = OnyxSteps.Stamp("profile", url, pointer, request.Version);
        Save(state, request.Port, steps, pointer);
        results.Add(new OnyxStepResult(OnyxSteps.Profile, written.Value ? OnyxStepOutcome.Ran : OnyxStepOutcome.Skipped));
        return new OnyxAccountResponse(url, pointer, written.Value, results);
    }

    private async ValueTask<OnyxStepOutcome> Signup(string url, Dictionary<string, string> steps, CancellationToken cancellationToken)
    {
        if (steps.ContainsKey(OnyxSteps.Signup))
        {
            progress.Line("signup: already done");
            return OnyxStepOutcome.Skipped;
        }

        if (!prompts.Interactive)
        {
            progress.Line($"signup: needs you at a terminal; open {url}/auth/signup and create your Onyx account, then run `sindri onyx up` again");
            return OnyxStepOutcome.NeedsHuman;
        }

        var page = new Uri($"{url}/auth/signup");
        if (!browser.Open(page))
        {
            progress.Line($"open {page} in your browser");
        }

        await prompts.Pause("Create your Onyx account in the browser (the first account becomes the admin), then press Enter", cancellationToken).ConfigureAwait(false);
        steps[OnyxSteps.Signup] = OnyxSteps.Stamp("signup");
        return OnyxStepOutcome.Ran;
    }

    private async ValueTask<Result<(string? Pointer, OnyxStepOutcome Outcome)>> Key(int port, Dictionary<string, string> steps, string? pointer, CancellationToken cancellationToken)
    {
        if (steps.ContainsKey(OnyxSteps.Key) && await minter.Stands(port, pointer, cancellationToken).ConfigureAwait(false))
        {
            progress.Line("key: already done");
            return (pointer, OnyxStepOutcome.Skipped);
        }

        if (!prompts.Interactive)
        {
            progress.Line("key: needs you at a terminal to ask for your Onyx login; run `sindri onyx up` there");
            return (null, OnyxStepOutcome.NeedsHuman);
        }

        var minted = await minter.Mint(port, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        steps[OnyxSteps.Key] = OnyxSteps.Stamp("key", minted.Value);
        return (minted.Value, OnyxStepOutcome.Ran);
    }

    private void Save(OnyxSetupState state, int port, Dictionary<string, string> steps, string? pointer)
        => workspace.WriteState(state with { Port = port, Steps = new Dictionary<string, string>(steps, StringComparer.Ordinal), KeyPointer = pointer });
}
