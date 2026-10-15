using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

public sealed class CheckMergeTests
{
    private readonly MetaWorld _world = new();

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo));

    private async Task SaveGate(string head, string main, string[] scopes, GateVerdict verdict = GateVerdict.Pass, int pr = 190, params GateStep[] red)
    {
        GateStep[] steps = [new("sync-rules", GateVerdict.Pass, "/l/sync.log"), .. red];
        _ = await _world.Gates.Save(new GateRecord(pr, head, main, scopes, steps, verdict, A.At(5)), CancellationToken.None);
    }

    // GitHub says: the pull request is at `head`, open, ready; origin/main is at `main`.
    private void Script(int pr = 190, string head = "headsha", string main = "mainsha", bool draft = false, string state = "OPEN")
    {
        var view = """{"headRefOid":"HEAD","isDraft":DRAFT,"title":"Add x","state":"STATE"}"""
            .Replace("HEAD", head, StringComparison.Ordinal).Replace("DRAFT", draft ? "true" : "false", StringComparison.Ordinal).Replace("STATE", state, StringComparison.Ordinal);
        _ = _world.Runner
            .When(string.Create(CultureInfo.InvariantCulture, $"gh pr view {pr} --json headRefOid,isDraft,title,state"), view)
            .When("rev-parse --verify --quiet origin/main", main + "\n")
            .When("mainsha...", "src/a.cs\n")   // the gated diff: code, so repo and dotnet
            .When("oldmain...", "src/a.cs\n")
            .When("git fetch");
    }

    private async Task<CheckMergeResponse> Check(int pr = 190)
    {
        var result = await Service().Handle(new CheckMergeRequest(pr), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_only_gate_does_not_vouch_for_a_code_change()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo"]);
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["scopes"], Failed(response), StringComparer.Ordinal);
        Assert.Contains("git diff --name-only mainsha...headsha", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_green_gate_on_the_current_head_and_main_is_ready()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script();

        var response = await Check();

        Assert.True(response.Ready);
        Assert.Equal(["gate", "gate-result", "scopes", "head", "main", "order", "draft", "state"], response.Checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Contains("git fetch --quiet origin pull/190/head", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_gate_it_is_refused_without_calling_git_or_gh()
    {
        await _world.ReadyWithGate();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Contains("gate", Failed(response), StringComparer.Ordinal);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_moved_since_the_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "src/a.cs\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_head_move_is_fine_once_a_repo_scope_gate_passed_on_the_new_head()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        await SaveGate("newhead", "mainsha", ["repo"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\nREADME.md\n");

        var response = await Check();

        Assert.True(response.Ready);
        Assert.Contains("docs-only", response.Checks.Single(c => c.Name == "head").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_head_move_without_a_later_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_later_repo_gate_does_not_excuse_a_code_change_in_the_delta()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        await SaveGate("newhead", "mainsha", ["repo"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\nsrc/b.cs\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Main_that_moved_since_the_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "oldmain", ["repo", "dotnet"]);
        Script(main: "newmain");

        Assert.Equal(["main"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_gate_is_refused_unless_every_red_step_is_a_classified_flake()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, red: new GateStep("unit", GateVerdict.Fail, "/l/unit.log") { Flake = FlakeVerdict.Real });
        Script();
        var refused = await Check();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, red: new GateStep("unit", GateVerdict.Fail, "/l/unit.log") { Flake = FlakeVerdict.Flake });

        var accepted = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.True(accepted.Ready);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("OPEN", false)]
    [InlineData("MERGED", true)]
    public async Task A_pull_request_must_wait_for_the_one_it_is_ordered_after(string earlierState, bool ready)
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], pr: 192);
        Script(pr: 192);
        _ = _world.Runner.When("gh pr view 190 --json state", $$"""{"state":"{{earlierState}}"}""");

        var response = await Check(pr: 192);

        Assert.Equal(ready, response.Ready);
        Assert.Equal(ready ? [] : ["order"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script(draft: true);

        Assert.Equal(["draft"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_that_is_no_longer_open_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script(state: "MERGED");

        Assert.Equal(["state"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new CheckMergeValidator().Validate(new CheckMergeRequest(0)).Error.Code);
}
