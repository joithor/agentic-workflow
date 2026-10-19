using Aw.Context.Core.Onyx;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// Onyx Community Edition has no service-account keys (the route is Business tier), so Sindri gets a dedicated admin user and a personal access token of its own:
// register it, make it an admin, log in as it, mint the token. Every step is safe to repeat, so a rerun after any failure picks up where it stopped.
public sealed class OnyxDedicatedUserTests
{
    private const string OwnerLogin = "owner-login-value-0042";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig()
        {
            Answer();
        }

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public void Answer() => Prompts.Answer("owner@example.com", OwnerLogin);

        public async Task<Kernel.Result<OnyxAccountResponse>> Run()
        {
            var minter = new OnyxKeyMinter(Prompts, Provisioning, Secrets, Secrets, new FakeRandomHex());
            return await new OnyxAccountService(Workspace, Prompts, new FakeBrowser(), minter, Profile, Progress).Handle(new OnyxAccountRequest(3000, "v4.9.0"), Token);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_dedicated_user_gets_a_random_password_that_satisfies_a_strict_policy_and_the_dedicated_email()
    {
        var rig = new Rig();

        _ = await rig.Run();

        var password = rig.Provisioning.Registered[OnyxSettings.DedicatedAccountEmail];
        Assert.True(password.Length is >= 20 and <= 64);
        Assert.Contains(password, static c => char.IsUpper(c));
        Assert.Contains(password, static c => char.IsLower(c));
        Assert.Contains(password, static c => char.IsDigit(c));
        Assert.Contains(password, static c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_existing_dedicated_user_is_reused_with_a_reset_password_and_no_second_user()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxSettings.DedicatedAccountEmail, "forgotten-password-1A!");

        var response = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Ran, response.Steps[1].Outcome);
        Assert.Equal(1, rig.Provisioning.Resets);
        _ = Assert.Single(rig.Provisioning.Registered);
        Assert.Contains(OnyxSettings.DedicatedAccountEmail, rig.Provisioning.Admins, StringComparer.Ordinal);
        Assert.NotNull(response.KeyPointer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_registration_is_sindri_onyx_008_makes_no_token_and_logs_out()
    {
        var rig = new Rig();
        rig.Provisioning.RegisterError = SindriErrors.OnyxKeyFailed.With("this Onyx is invite-only");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.Made);
        Assert.Empty(rig.Secrets.Stored);
        Assert.Equal(1, rig.Provisioning.LoggedOut);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_password_reset_that_fails_is_returned_and_makes_no_token()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxSettings.DedicatedAccountEmail, "x");
        rig.Provisioning.ResetError = SindriErrors.OnyxNotAdmin.With("not an admin");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxNotAdmin.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.Made);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dedicated_login_that_is_refused_is_sindri_onyx_008_and_names_the_dedicated_user()
    {
        var rig = new Rig();
        rig.Provisioning.RegisterError = null;
        var failing = new FailDedicatedLogin(rig.Provisioning);

        var minter = new OnyxKeyMinter(rig.Prompts, failing, rig.Secrets, rig.Secrets, new FakeRandomHex());
        var result = await new OnyxAccountService(rig.Workspace, rig.Prompts, new FakeBrowser(), minter, rig.Profile, rig.Progress).Handle(new OnyxAccountRequest(3000, "v4.9.0"), Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains(OnyxSettings.DedicatedAccountEmail, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_that_cannot_be_made_is_returned_and_a_rerun_resumes_at_the_key_step_without_the_sign_up_again()
    {
        var rig = new Rig();
        var pauses = 0;
        rig.Prompts.OnPause = () => pauses++;
        rig.Provisioning.TokenError = SindriErrors.OnyxKeyFailed.With("Onyx didn't make the token");
        var failed = await rig.Run();
        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, failed.Error.Code);
        Assert.True(rig.Workspace.State!.Steps.ContainsKey("signup"));
        Assert.False(rig.Workspace.State.Steps.ContainsKey("key"));
        rig.Provisioning.TokenError = null;
        rig.Answer();

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[1].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[2].Outcome);
        Assert.Equal(1, pauses);   // the sign-up page wait is not repeated
        _ = Assert.Single(rig.Provisioning.Registered);   // the user made by the first try is reused
        Assert.Equal(1, rig.Provisioning.Resets);
    }

    // A provisioning whose login as the dedicated user is refused while the owner's works.
    private sealed class FailDedicatedLogin(FakeOnyxProvisioning inner) : IOnyxProvisioning
    {
        public ValueTask<Kernel.Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken)
            => string.Equals(email, OnyxSettings.DedicatedAccountEmail, StringComparison.Ordinal)
                ? ValueTask.FromResult<Kernel.Result<IOnyxSession>>(SindriErrors.OnyxKeyFailed.With("Onyx refused that login (HTTP 400)"))
                : inner.SignIn(port, email, password, cancellationToken);

        public ValueTask<Kernel.Result<bool>> TokenIsAdmin(int port, string token, string email, CancellationToken cancellationToken)
            => inner.TokenIsAdmin(port, token, email, cancellationToken);
    }
}
