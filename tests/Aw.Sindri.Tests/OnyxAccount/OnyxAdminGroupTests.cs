using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// Onyx Community Edition answers the user-group list with 402 (it is Enterprise-only): the default Admin group's id then comes from Sindri's own stack's database,
// and the key that is made with it is only kept when Onyx says its service account really is in the Admin group.
public sealed class OnyxAdminGroupTests
{
    private const string OwnerLogin = "owner-login-value-0042";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig()
        {
            Prompts.Answer("owner@example.com", OwnerLogin);
            Provisioning.GroupError = SindriErrors.OnyxGroupListUnavailable.With("Onyx answered 402");
            Database.Group = 7;
        }

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeOnyxDatabase Database { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public async Task<Kernel.Result<OnyxAccountResponse>> Run()
        {
            var facts = new OnyxFacts(new FakeContainerRuntime(FakeContainerRuntime.Healthy), new FakeHostProbes(), new FakeSindriHome(OnyxUpRig.Home));
            var minter = new OnyxKeyMinter(Prompts, Provisioning, Secrets, Secrets, new OnyxAdminGroup(Database, facts));
            var service = new OnyxAccountService(Workspace, Prompts, new FakeBrowser(), minter, Profile, Progress);
            return await service.Handle(new OnyxAccountRequest(3000, "v4.9.0"), Token);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_402_group_list_takes_the_admin_group_from_the_stacks_own_database_and_the_key_is_made_with_it()
    {
        var rig = new Rig();

        var response = (await rig.Run()).Value;

        Assert.Equal([7], rig.Provisioning.KeyGroups);
        Assert.Equal(ComposeProject.NameFor(OnyxUpRig.Home), Assert.Single(rig.Database.Asked));
        Assert.Equal("keychain:onyx/sindri-onyx", response.KeyPointer);
        Assert.Equal(1, rig.Provisioning.LoggedOut);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_group_list_that_works_never_reads_the_database()
    {
        var rig = new Rig();
        rig.Provisioning.GroupError = null;

        var response = (await rig.Run()).Value;

        Assert.Empty(rig.Database.Asked);
        Assert.Equal([1], rig.Provisioning.KeyGroups);
        Assert.NotNull(response.KeyPointer);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-ONYX-007")]
    [InlineData("SND-ONYX-008")]
    public async Task Any_other_group_list_failure_is_returned_as_it_is_and_the_database_is_not_read(string code)
    {
        var rig = new Rig();
        rig.Provisioning.GroupError = code == "SND-ONYX-007" ? SindriErrors.OnyxNotAdmin.With("403") : SindriErrors.OnyxKeyFailed.With("500");

        var result = await rig.Run();

        Assert.Equal(code, result.Error.Code);
        Assert.Empty(rig.Database.Asked);
        Assert.Empty(rig.Provisioning.KeyGroups);
        Assert.Empty(rig.Secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_stack_of_sindris_own_nothing_is_read_and_the_error_names_the_manual_key_path()
    {
        var rig = new Rig();
        rig.Database.Group = null;

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("Community Edition", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("docs/sindri/onyx.md", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Provisioning.KeyGroups);
        Assert.Empty(rig.Secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_database_that_cannot_be_read_returns_its_own_error_and_makes_no_key()
    {
        var rig = new Rig();
        rig.Database.Failure = SindriErrors.OnyxDockerUnavailable.With("Docker isn't running");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxDockerUnavailable.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.KeyGroups);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_whose_service_account_is_not_in_the_admin_group_is_not_stored_and_the_error_keeps_what_onyx_said()
    {
        var rig = new Rig();
        rig.Provisioning.CreateKeyError = SindriErrors.OnyxKeyFailed.With("Onyx made the key on_…ab12 (sindri-onyx) but its service account is not in the Admin group; delete it on Onyx's admin API keys page");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("delete it", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Secrets.Stored);
        Assert.Null(rig.Workspace.State!.KeyPointer);
        Assert.Null(rig.Profile.Section);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_after_the_group_step_failed_resumes_at_the_key_step_without_the_sign_up_again()
    {
        var rig = new Rig();
        var pauses = 0;
        rig.Prompts.OnPause = () => pauses++;
        rig.Database.Group = null;
        _ = await rig.Run();
        Assert.True(rig.Workspace.State!.Steps.ContainsKey("signup"));
        Assert.False(rig.Workspace.State.Steps.ContainsKey("key"));
        rig.Database.Group = 7;
        rig.Prompts.Answer("owner@example.com", OwnerLogin);

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[1].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[2].Outcome);
        Assert.Equal(1, pauses);   // no second wait for the sign-up page
        Assert.Equal([7], rig.Provisioning.KeyGroups);
    }
}
