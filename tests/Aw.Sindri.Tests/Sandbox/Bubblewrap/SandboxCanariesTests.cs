using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The self-check (sandbox spec §6) as scripts built from the plan, and how a failing one becomes SND-LAUNCH-016 with the canary's name.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxCanariesTests
{
    private static readonly BubblewrapOptions _options = new("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5));

    // Nothing in the plan needs a directory made on this machine: only /dev and /proc.
    private static SandboxPlan Bare(SandboxPlan plan) => plan with { Mounts = [.. plan.Mounts.Where(m => m.Kind is PlanMountKind.Dev or PlanMountKind.Proc)], Environment = [] };

    private static string[] Reasons(SandboxPlan plan) => [.. SandboxCanaries.For(plan).Select(c => c.Reason)];

    [Fact]
    public void A_write_scope_plan_has_every_canary_this_adapter_owns_in_order() =>
        Assert.Equal(["fs-deny", "fs-write", "git-ref", "env", "login", "hooks-run", "net-deny"], Reasons(SandboxPlannerTests.Build(Req.WriteScope)));

    [Fact]
    public void A_read_only_plan_has_no_git_ref_canary_and_proves_the_worktree_is_not_writable()
    {
        var plan = SandboxPlannerTests.Build(Req.ReadOnly);

        Assert.DoesNotContain("git-ref", Reasons(plan), StringComparer.Ordinal);
        Assert.Contains("&& fail", SandboxCanaries.For(plan).Single(c => c.Reason == "fs-write").Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Git_canary_never_deletes_a_ref_and_checks_the_effective_settings_on_the_real_worktree()
    {
        var script = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "git-ref").Script;

        Assert.DoesNotContain("-d ", script, StringComparison.Ordinal);
        Assert.DoesNotContain("branch", script, StringComparison.Ordinal);
        Assert.Contains($"cd '{Req.Worktree}'", script, StringComparison.Ordinal);
        Assert.Contains("git config --get core.hooksPath", script, StringComparison.Ordinal);
        Assert.Contains("refs/heads/sindri/x/1/selfcheck", script, StringComparison.Ordinal);
        Assert.Contains("refs/heads/other-selfcheck HEAD >/dev/null 2>&1 && fail", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hook_canary_has_the_three_parts_and_runs_under_bash()
    {
        var canary = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "hooks-run");

        Assert.Equal("/bin/bash", canary.Shell);
        Assert.Contains("block-destructive.sh", canary.Script, StringComparison.Ordinal);
        Assert.Contains("detect-secrets.sh", canary.Script, StringComparison.Ordinal);
        Assert.Contains("judge brief get task:selfcheck", canary.Script, StringComparison.Ordinal);
        Assert.Contains("judge/sessions", canary.Script, StringComparison.Ordinal);
        Assert.Contains("aw_failopen", canary.Script, StringComparison.Ordinal);
        Assert.Contains("meta-outbox.sh", canary.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_login_canary_checks_the_copy_the_plan_names_and_the_originals_mask()
    {
        var script = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "login").Script;

        Assert.Contains($"'{Req.Home}/.claude-run/.credentials.json'", script, StringComparison.Ordinal);
        Assert.Contains("= 600", script, StringComparison.Ordinal);
        Assert.Contains("$HOME/.claude/.credentials.json", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_env_canary_flags_a_name_off_the_allowlist_and_the_held_slot_marker()
    {
        var canary = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "env");

        Assert.Null(canary.Judge!(FakeProcesses.Ok("PATH=/bin\nHOME=/h\nAW_SANDBOX=1\n")));
        Assert.Contains("GH_TOKEN", canary.Judge!(FakeProcesses.Ok("PATH=/bin\nGH_TOKEN=x\n")), StringComparison.Ordinal);
        Assert.Contains("AW_HEAVY_LOCK_HELD", canary.Judge!(FakeProcesses.Ok("PATH=/bin\nAW_HEAVY_LOCK_HELD=1\n")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("it's", "'it'\\''s'")]
    [InlineData("a b", "'a b'")]
    public void Quote_closes_escapes_and_reopens_single_quotes(string value, string expected) => Assert.Equal(expected, SandboxCanaries.Quote(value));

    [Fact]
    public async Task A_failing_canary_is_016_naming_the_canary_and_what_was_observed()
    {
        var plan = Bare(SandboxPlannerTests.Build(Req.ReadOnly));
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Failed("visible: /home/owner/.ssh"));
        var sandbox = new BubblewrapSandbox(_options, processes, new HostPaths(), static _ => null);

        var result = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Equal("fs-deny", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("visible: /home/owner/.ssh", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_selfcheck_ref_is_removed_from_outside_after_a_write_scope_self_check_even_when_a_canary_fails()
    {
        var built = SandboxPlannerTests.Build(Req.WriteScope);
        var plan = Bare(built) with { Worktree = Req.Worktree, Mounts = [.. Bare(built).Mounts, new PlanMount(Req.Worktree, PlanMountKind.ReadWrite, Optional: true)] };
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Failed("boom"));
        _ = processes.When((p, _) => p == "git", FakeProcesses.Ok());
        var sandbox = new BubblewrapSandbox(_options, processes, new HostPaths(), static _ => null);

        _ = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        var git = processes.Calls.Single(c => c.Program == "git");
        Assert.Equal(["-C", Req.Worktree, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=", "update-ref", "-d", "refs/heads/sindri/x/1/selfcheck"], git.Arguments);
    }

    [Fact]
    public async Task A_login_whose_copy_directory_the_inner_policy_does_not_deny_fails_the_login_canary_before_anything_runs()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        var open = plan with { Policy = plan.Policy with { Rules = [.. plan.Policy.Rules.Where(r => !(r.Effect == InnerRuleEffect.Deny && r.Tool == "Read" && r.Pattern.StartsWith(Req.Home + "/.claude-run", StringComparison.Ordinal)))] } };
        var processes = new FakeProcesses();

        var result = await new BubblewrapSandbox(_options, processes, new HostPaths(), static _ => null).SelfCheck(open, TestContext.Current.CancellationToken);

        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public void Wrap_puts_bwrap_first_the_argv_after_dash_dash_and_asks_the_host_for_the_launch_secret()
    {
        var plan = SandboxPlannerTests.Build(Req.ReadOnly);
        var bare = Bare(plan) with { Environment = [.. plan.Environment.Where(e => e.Value is null)] };
        var sandbox = new BubblewrapSandbox(_options, new FakeProcesses(), new HostPaths(), name => string.Equals(name, "AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal) ? "s3" : null);

        var argv = sandbox.Wrap(bare, ["claude", "--x"]).Value;

        Assert.Equal("bwrap", argv[0]);
        Assert.Equal(["--", "claude", "--x"], argv.TakeLast(3), StringComparer.Ordinal);
        Assert.Contains("s3", argv, StringComparer.Ordinal);
    }
}
