using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>The inspector over a scripted git and gh and a fake process scan: the facts that no real-git test can set up cheaply.</summary>
public sealed class WorktreeInspectorTests
{
    private const string Gate = "/state/meta/gates/7/tree";
    private readonly MetaWorld _world = new();

    private static TeamConfig Team() => new("t", "/work", ["api"], "meta");

    // A repo with the main worktree and one gate tree; pull request 7 in the given state; the gate tree's HEAD has `notInMain` commits main lacks.
    private void Script(string state = "MERGED", int notInMain = 0, string committer = "someone@example.com")
    {
        _ = _world.Runner
            .When("git fetch", string.Empty)
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("worktree list --porcelain", "worktree /work\nHEAD aaaaaaa\nbranch refs/heads/main\n\nworktree " + Gate + "\nHEAD bbbbbbb\ndetached\n")
            .When("gh pr list", "[{\"number\":7,\"state\":\"" + state + "\",\"headRefName\":\"feat\",\"headRefOid\":\"ccccccc\"}]")
            .When("--not origin/main", notInMain.ToString(CultureInfo.InvariantCulture) + "\n")
            .When("--not --remotes", "0\n")
            .When("status --porcelain", string.Empty)
            .When("rev-parse --show-toplevel", _ => new CommandResult(0, Gate + "\n", string.Empty, TimedOut: false))
            .When("log -1 --format=%ce", committer + "\n");
    }

    private async Task<WorktreeReport> GateTreeReport()
    {
        var result = await _world.Inspector.Inspect(Team(), WorktreeScope.All, fetch: true, onlyPath: null, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return Assert.Single(result.Value, w => w.Path == Gate);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_of_a_merged_pull_request_is_removable()
    {
        Script();

        var report = await GateTreeReport();

        Assert.Equal((WorktreeDecision.Removable, 7), (report.Decision, report.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_is_kept_while_a_gate_run_of_its_pull_request_is_running()
    {
        Script();
        _world.Locations.Add(new ProcessLocation(500, "/usr/bin/aw-meta gate-run 7 dotnet", ["/elsewhere"]));

        var report = await GateTreeReport();

        Assert.Equal(WorktreeDecision.Kept, report.Decision);
        Assert.Contains(report.Reasons, r => r.Contains("running", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_command_that_asked_for_the_sweep_and_its_ancestors_do_not_count_as_a_running_gate()
    {
        Script();
        _world.Locations.Add(new ProcessLocation(500, "bash -c aw-meta merge-run 7", ["/elsewhere"], IsInvoker: true));

        Assert.Equal(WorktreeDecision.Removable, (await GateTreeReport()).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_whose_head_is_the_gates_own_merge_commit_is_removable_once_its_pull_request_closed()
    {
        Script(state: "CLOSED", notInMain: 3, committer: "aw-meta@localhost");

        Assert.Equal(WorktreeDecision.Removable, (await GateTreeReport()).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_with_commits_that_are_not_the_gates_own_is_kept()
    {
        Script(state: "CLOSED", notInMain: 3);

        Assert.Equal(WorktreeDecision.Kept, (await GateTreeReport()).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_open_inside_a_worktree_keeps_it()
    {
        Script();
        _world.Locations.Add(new ProcessLocation(600, "nvim notes.md", ["/elsewhere", Gate + "/notes.md"]));

        var report = await GateTreeReport();

        Assert.Equal(WorktreeDecision.Kept, report.Decision);
        Assert.Contains(report.Reasons, r => r.Contains("600", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_main_worktree_and_the_shared_checkout_are_never_removable()
    {
        Script();

        var result = await _world.Inspector.Inspect(Team(), WorktreeScope.All, fetch: true, onlyPath: null, CancellationToken.None);

        var main = Assert.Single(result.Value, w => w.Path == "/work");
        Assert.Equal(WorktreeDecision.Kept, main.Decision);
        Assert.Contains(main.Reasons, r => r.Contains("shared checkout", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_the_fetch_or_the_process_scan_fails_nothing_is_judged()
    {
        _ = _world.Runner.When("git fetch", string.Empty, exitCode: 1);
        var fetchFailed = await _world.Inspector.Inspect(Team(), WorktreeScope.All, fetch: true, onlyPath: null, CancellationToken.None);

        var scan = new MetaWorld();
        scan.Locations.Failure = MetaErrors.HostUnreadable.With("no /proc");
        _ = scan.Runner.When("git fetch", string.Empty).When("rev-parse --verify --quiet origin/main", "m\n").When("worktree list --porcelain", "worktree /work\nHEAD a\nbranch refs/heads/main\n").When("gh pr list", "[]");
        var scanFailed = await scan.Inspector.Inspect(Team(), WorktreeScope.All, fetch: true, onlyPath: null, CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, fetchFailed.Error.Code);
        Assert.Equal(MetaErrors.HostUnreadable.Code, scanFailed.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_worktree_git_cannot_read_is_kept_with_the_reason_not_dropped()
    {
        var broken = new MetaWorld();
        _ = broken.Runner.When("git fetch", string.Empty).When("rev-parse --verify --quiet origin/main", "m\n").When("worktree list --porcelain", "worktree /work\nHEAD a\nbranch refs/heads/main\n\nworktree /wt/x\nHEAD b\nbranch refs/heads/x\n").When("gh pr list", "[]")
            .When("--not", "0\n").When("status --porcelain", string.Empty, exitCode: 128);

        var result = await broken.Inspector.Inspect(Team(), WorktreeScope.All, fetch: true, onlyPath: null, CancellationToken.None);

        var unreadable = Assert.Single(result.Value, w => w.Path == "/wt/x");
        Assert.Equal(WorktreeDecision.Kept, unreadable.Decision);
        Assert.Contains(unreadable.Reasons, r => r.Contains("could not inspect", StringComparison.Ordinal));
    }
}
