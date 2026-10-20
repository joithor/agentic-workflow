using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>The pure decision of each setup step: every state it can be in, the plain words, and the action each state points at.</summary>
public sealed class SetupStepsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Repo_is_todo_with_AddRepo_until_the_profile_names_one()
    {
        var todo = SetupSteps.Repo(new RepoFact(0));
        var done = SetupSteps.Repo(new RepoFact(2));

        Assert.Equal((SetupState.Todo, SetupActions.AddRepo), (todo.State, todo.Action));
        Assert.Equal((SetupState.Done, null, "2 repositories added."), (done.State, done.Action, done.Detail));
        Assert.Equal("1 repository added.", SetupSteps.Repo(new RepoFact(1)).Detail);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ProfilePresence.Missing, SetupState.Todo, null)]
    [InlineData(ProfilePresence.Unapproved, SetupState.NeedsYou, SetupActions.ApproveProfile)]
    [InlineData(ProfilePresence.Drifted, SetupState.NeedsYou, SetupActions.ApproveProfile)]
    [InlineData(ProfilePresence.Approved, SetupState.Done, null)]
    public void Profile_asks_the_human_when_it_is_waiting_or_has_changed(ProfilePresence presence, SetupState state, string? action)
    {
        var step = SetupSteps.Profile(new ProfileFact(presence));

        Assert.Equal((state, action), (step.State, step.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Recipes_count_what_waits_and_carry_the_progress()
    {
        var none = SetupSteps.Recipes(new RecipesFact(0, 0));
        var waiting = SetupSteps.Recipes(new RecipesFact(26, 20));
        var done = SetupSteps.Recipes(new RecipesFact(26, 26));

        Assert.Equal((SetupState.Todo, null, null), (none.State, none.Action, none.Progress));
        Assert.Equal((SetupState.NeedsYou, SetupActions.ApproveAllRecipes, new SetupProgress(20, 26), "6 workflows waiting for approval."), (waiting.State, waiting.Action, waiting.Progress, waiting.Detail));
        Assert.Equal((SetupState.Done, null, new SetupProgress(26, 26)), (done.State, done.Action, done.Progress));
        Assert.Equal("1 workflow waiting for approval.", SetupSteps.Recipes(new RecipesFact(3, 2)).Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Mcp_waits_for_a_repo_then_is_done_when_every_repo_is_registered()
    {
        var waiting = SetupSteps.Mcp(new McpFact(0, 0));
        var todo = SetupSteps.Mcp(new McpFact(1, 0));
        var some = SetupSteps.Mcp(new McpFact(3, 1));
        var done = SetupSteps.Mcp(new McpFact(3, 3));

        Assert.Equal((SetupState.Todo, null), (waiting.State, waiting.Action));
        Assert.Equal((SetupState.Todo, SetupActions.RegisterMcp, null), (todo.State, todo.Action, todo.Progress));
        Assert.Equal((SetupState.Todo, SetupActions.RegisterMcp, new SetupProgress(1, 3)), (some.State, some.Action, some.Progress));
        Assert.Equal((SetupState.Done, null), (done.State, done.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Map_runs_on_its_own_so_it_never_has_an_action()
    {
        var steps = new[]
        {
            SetupSteps.Map(new MapFact(MapPhase.Waiting)),
            SetupSteps.Map(new MapFact(MapPhase.NotBuilt)),
            SetupSteps.Map(new MapFact(MapPhase.Building)),
            SetupSteps.Map(new MapFact(MapPhase.Building, new SetupProgress(40, 100))),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040")),
            SetupSteps.Map(new MapFact(MapPhase.Built)),
        };

        Assert.Equal([SetupState.Todo, SetupState.Todo, SetupState.Running, SetupState.Running, SetupState.Failed, SetupState.Done], steps.Select(static s => s.State));
        Assert.All(steps, static s => Assert.Null(s.Action));
        Assert.Equal(new SetupProgress(40, 100), steps[3].Progress);
        Assert.Equal("Sindri is building the map: 40 of 100 done.", steps[3].Detail);
        Assert.Equal("SND-CONTEXT-040", steps[4].Error?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_is_done_when_up_todo_with_SetupOnyx_when_it_can_be_set_up_and_failed_when_it_cannot()
    {
        var done = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null));
        var notSetUp = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null));
        var stopped = SetupSteps.Onyx(new OnyxFact(OnyxState.Stopped, Unreachable: false, BlockedByCode: null));
        var noDocker = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-001"));
        var unknown = SetupSteps.Onyx(new OnyxFact(OnyxState.Unknown, Unreachable: false, BlockedByCode: null));
        var silent = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: true, BlockedByCode: null));

        Assert.Equal(SetupState.Done, done.State);
        Assert.Equal((SetupState.Todo, SetupActions.SetupOnyx), (notSetUp.State, notSetUp.Action));
        Assert.Equal((SetupState.Todo, "Onyx is set up but stopped."), (stopped.State, stopped.Detail));
        Assert.Equal((SetupState.Failed, "SND-ONYX-001", null), (noDocker.State, noDocker.Error?.Code, noDocker.Action));
        Assert.Equal((SetupState.Failed, "SND-ONYX-001"), (unknown.State, unknown.Error?.Code));
        Assert.Equal((SetupState.Failed, "SND-ONYX-005"), (silent.State, silent.Error?.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_step_keeps_its_code_out_of_every_plain_text()
    {
        List<SetupStep> steps =
        [
            SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-006")),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040")),
            .. Enum.GetValues<SetupStepId>().Select(static id => SetupSteps.Unreadable(id, "SND-LEDGER-002")),
        ];

        Assert.All(steps, static s =>
        {
            Assert.Equal(SetupState.Failed, s.State);
            Assert.Equal(s.Error?.Sentence, s.Detail);
            Assert.DoesNotContain("SND-", s.Title + s.Why + s.Detail + s.Error?.Sentence, StringComparison.Ordinal);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_step_has_a_title_and_a_reason_in_every_state()
    {
        var all = new[]
        {
            SetupSteps.Repo(new RepoFact(0)), SetupSteps.Profile(new ProfileFact(ProfilePresence.Missing)), SetupSteps.Recipes(new RecipesFact(0, 0)),
            SetupSteps.Mcp(new McpFact(0, 0)), SetupSteps.Map(new MapFact(MapPhase.Waiting)), SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null)),
        };

        Assert.Equal(Enum.GetValues<SetupStepId>(), all.Select(static s => s.Id));
        Assert.All(all, static s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.EndsWith(".", s.Why, StringComparison.Ordinal);
        });
    }
}
