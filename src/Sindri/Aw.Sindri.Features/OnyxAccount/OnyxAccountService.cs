using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 6-9. (6) The owner makes their own account in Onyx's sign-up page (Onyx makes the first user the admin; Sindri registers nobody). (7) With that login, asked for once at the terminal
/// and forgotten, Sindri makes its admin key and checks it. (8) The key goes to the OS secret store. (9) The profile section is written. Each step is recorded in state.json once done, so a rerun skips it; a recorded
/// key is also checked against the secret store and Onyx before it is trusted. Without a terminal nothing is asked: the stage ends "needs you".
/// </summary>
internal sealed class OnyxAccountService(IOnyxWorkspace workspace, IOnyxPrompts prompts, IBrowser browser, IOnyxProvisioning provisioning, ISecretWriter secrets,
    ISecretPointers pointers, IOnyxProfileWriter profile, IOnyxProgress progress) : IService<OnyxAccountRequest, OnyxAccountResponse>
{
    private const string KeyName = "sindri-onyx";
    private const string KeyService = "onyx";

    public async ValueTask<Result<OnyxAccountResponse>> Handle(OnyxAccountRequest request, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{request.Port}");
        var state = workspace.ReadState() ?? new OnyxSetupState(request.Port, new Dictionary<string, string>(StringComparer.Ordinal));
        var steps = new Dictionary<string, string>(state.Steps, StringComparer.Ordinal);
        var pointer = state.KeyPointer;
        List<OnyxStepResult> results = [];

        var signup = await Signup(url, steps, cancellationToken).ConfigureAwait(false);
        results.Add(new OnyxStepResult(OnyxSteps.Signup, signup));
        if (signup == OnyxStepOutcome.NeedsHuman)
        {
            return new OnyxAccountResponse(url, pointer, ProfileChanged: false, results);
        }

        Save(state, request.Port, steps, pointer);
        var key = await Key(request.Port, steps, pointer, cancellationToken).ConfigureAwait(false);
        if (key.IsError)
        {
            return key.Error;
        }

        (pointer, var keyOutcome) = key.Value;
        results.Add(new OnyxStepResult(OnyxSteps.Key, keyOutcome));
        if (keyOutcome == OnyxStepOutcome.NeedsHuman)
        {
            return new OnyxAccountResponse(url, pointer, ProfileChanged: false, results);
        }

        if (keyOutcome == OnyxStepOutcome.Ran)
        {
            steps[OnyxSteps.Key] = OnyxSteps.Stamp("key", pointer!);
            Save(state, request.Port, steps, pointer);
        }

        var written = profile.SetOnyx(url, pointer!, request.Version);
        if (written.IsError)
        {
            return written.Error;
        }

        steps[OnyxSteps.Profile] = OnyxSteps.Stamp("profile", url, pointer!, request.Version);
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
        if (await RecordedKeyStands(port, steps, pointer, cancellationToken).ConfigureAwait(false))
        {
            progress.Line("key: already done");
            return (pointer, OnyxStepOutcome.Skipped);
        }

        if (!prompts.Interactive)
        {
            progress.Line("key: needs you at a terminal to ask for your Onyx login; run `sindri onyx up` there");
            return (pointer, OnyxStepOutcome.NeedsHuman);
        }

        var minted = await Mint(port, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        var stored = secrets.Store(KeyService, KeyName, minted.Value);
        return stored.IsError ? stored.Error : (stored.Value.ToString(), OnyxStepOutcome.Ran);
    }

    // The key is trusted again only when it was recorded, still resolves from where it was stored, and Onyx still takes it as an admin service account.
    private async ValueTask<bool> RecordedKeyStands(int port, Dictionary<string, string> steps, string? pointer, CancellationToken cancellationToken)
    {
        if (!steps.ContainsKey(OnyxSteps.Key) || pointer is null || SecretPointer.Parse(pointer) is not { IsSuccess: true } parsed)
        {
            return false;
        }

        var key = pointers.Resolve(parsed.Value);
        if (key.IsError)
        {
            return false;
        }

        var admin = await provisioning.KeyIsAdmin(port, key.Value, cancellationToken).ConfigureAwait(false);
        return admin is { IsSuccess: true, Value: true };
    }

    // The owner's login is read into memory for this call only; the session logs out when it goes out of scope.
    private async ValueTask<Result<string>> Mint(int port, CancellationToken cancellationToken)
    {
        var email = await prompts.Ask("Your Onyx email: ", cancellationToken).ConfigureAwait(false);
        var password = email is { Length: > 0 } ? await prompts.AskSecret("Your Onyx password (not shown, not stored): ", cancellationToken).ConfigureAwait(false) : null;
        if (email is not { Length: > 0 } || password is not { Length: > 0 })
        {
            return SindriErrors.OnyxKeyFailed.With("no Onyx login was given; run `sindri onyx up` again and enter the email and password of the account you created");
        }

        var signedIn = await provisioning.SignIn(port, email, password, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return signedIn.Error;
        }

        await using var session = signedIn.Value;
        var group = await session.AdminGroup(cancellationToken).ConfigureAwait(false);
        if (group.IsError)
        {
            return group.Error;
        }

        var made = await session.CreateKey(KeyName, group.Value, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var admin = await provisioning.KeyIsAdmin(port, made.Value, cancellationToken).ConfigureAwait(false);
        if (admin.IsError)
        {
            return admin.Error;
        }

        return admin.Value
            ? made.Value
            : SindriErrors.OnyxKeyFailed.With("Onyx made the key but doesn't treat it as an admin service account; check the account's group in Onyx's admin users page, then run `sindri onyx up` again");
    }

    private void Save(OnyxSetupState state, int port, Dictionary<string, string> steps, string? pointer)
        => workspace.WriteState(state with { Port = port, Steps = new Dictionary<string, string>(steps, StringComparer.Ordinal), KeyPointer = pointer });
}
