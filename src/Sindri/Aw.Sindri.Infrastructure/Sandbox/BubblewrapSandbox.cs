using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The Linux sandbox (sandbox spec §3.1, §4, SB12): a plan from the pure builder, turned into a bubblewrap command line, and proven inside the real thing by the canaries.
/// The cgroup scope (SB-3a) and the network forwarders (SB-4a) wrap and join it later; this adapter is the mount stack, the environment and the self-check.
/// </summary>
internal sealed class BubblewrapSandbox(BubblewrapOptions options, ISandboxProcesses processes, IHostPaths host, Func<string, string?> secretOf) : ISandbox
{
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(10);

    private static readonly string[] _controllers = ["memory", "cpu", "pids"];

    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe()
    {
        var bwrap = processes.Run(options.Bwrap, ["--version"], _probeTimeout);
        if (!bwrap.Succeeded)
        {
            return Unavailable(SandboxReasons.NoBwrap, "bubblewrap is not installed");
        }

        var userNamespace = processes.Run(options.Bwrap, ["--ro-bind", "/", "/", "--unshare-user", "--unshare-net", "true"], _probeTimeout);
        if (!userNamespace.Succeeded)
        {
            return Unavailable(SandboxReasons.NoUserNamespace, FirstLine(userNamespace.Error, "bubblewrap cannot create a sandbox here"));
        }

        var scope = processes.Run(options.SystemdRun, ["--user", "--scope", "--quiet", "true"], _probeTimeout);
        if (!scope.Succeeded)
        {
            return Unavailable(SandboxReasons.NoUserManager, FirstLine(scope.Error, "systemd-run --user --scope does not work for this account"));
        }

        if (MissingController() is { } missing)
        {
            return Unavailable(SandboxReasons.NoCgroupControllers, $"the account's cgroup lacks the {missing} controller");
        }

        if (!processes.Run(options.Socat, ["-V"], _probeTimeout).Succeeded)
        {
            return Unavailable(SandboxReasons.NoForwarder, "socat is not installed");
        }

        return new SandboxCapabilities(SandboxMechanism.Bubblewrap, FirstLine(bwrap.Output, "bubblewrap"));
    }

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => SandboxPlanner.Build(SandboxMechanism.Bubblewrap, request);

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(argv);
        try
        {
            RunAreaPreparer.Prepare(plan);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the run area could not be prepared: " + e.Message);
        }

        return Results.Success<IReadOnlyList<string>>([options.Bwrap, .. BubblewrapArgv.Build(plan, host, secretOf), "--", .. argv]);
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (PolicyHidesTheLogin(plan) is { } refusal)
        {
            return refusal;
        }

        try
        {
            foreach (var canary in SandboxCanaries.For(plan))
            {
                ct.ThrowIfCancellationRequested();
                var observed = await Task.Run(() => Run(plan, canary), ct).ConfigureAwait(false);
                if (observed is not null)
                {
                    return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, canary.Reason, observed);
                }
            }
        }
        finally
        {
            RemoveSelfCheckRef(plan);
        }

        return Unit.Value;
    }

    // The login canary also proves, on the plan, that the inner policy refuses the agent's tools the config directory.
    private static Error? PolicyHidesTheLogin(SandboxPlan plan)
    {
        var configs = plan.Environment
            .Where(e => string.Equals(e.Name, "CLAUDE_CONFIG_DIR", StringComparison.Ordinal) || string.Equals(e.Name, "CODEX_HOME", StringComparison.Ordinal))
            .Select(e => e.Value)
            .OfType<string>();
        var open = configs.FirstOrDefault(dir => !plan.Policy.Rules.Any(r => r.Effect == InnerRuleEffect.Deny && string.Equals(r.Tool, "Read", StringComparison.Ordinal) && r.Pattern.StartsWith(dir, StringComparison.Ordinal)));
        return open is null ? null : SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Login, $"the inner policy does not deny reading {open}");
    }

    private string? Run(SandboxPlan plan, SandboxCanary canary)
    {
        var wrapped = Wrap(plan, [canary.Shell, "-c", canary.Script]);
        if (wrapped.IsError)
        {
            return wrapped.Error.Message;
        }

        var result = processes.Run(wrapped.Value[0], [.. wrapped.Value.Skip(1)], options.CanaryTimeout);
        if (!result.Started)
        {
            return "bubblewrap could not be started";
        }

        if (result.TimedOut)
        {
            return $"timed out after {options.CanaryTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s";
        }

        if (canary.Judge is { } judge)
        {
            return result.ExitCode == 0 ? judge(result) : Observed(result);
        }

        return result.ExitCode == 0 ? null : Observed(result);
    }

    private static string Observed(ProcessResult result)
    {
        var text = (result.Output + result.Error).Trim();
        var line = FirstLine(text, $"exit {result.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        return line.Length > 300 ? line[..300] : line;
    }

    // The git-ref canary never deletes a ref from inside; the ref is removed from outside, as the owner.
    private void RemoveSelfCheckRef(SandboxPlan plan)
    {
        if (plan.Worktree is null || !plan.Writable.Contains(plan.Worktree, StringComparer.Ordinal))
        {
            return;
        }

        _ = processes.Run(options.Git, ["-C", plan.Worktree, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=", "update-ref", "-d", $"refs/heads/{plan.RunNamespace}/selfcheck"], _probeTimeout);
    }

    private static string? MissingController()
    {
        var uid = Environment.UserName.Length > 0 ? GetUid() : null;
        var file = uid is null ? null : $"/sys/fs/cgroup/user.slice/user-{uid}.slice/user@{uid}.service/cgroup.controllers";
        if (file is null || !File.Exists(file))
        {
            return null;   // not readable here: the user-scope probe above is the test
        }

        var have = File.ReadAllText(file).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _controllers.FirstOrDefault(c => !have.Contains(c, StringComparer.Ordinal));
    }

    private static string? GetUid() => File.Exists("/proc/self/loginuid") ? File.ReadAllText("/proc/self/loginuid").Trim() : null;

    private static string FirstLine(string text, string fallback) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? fallback;

    private static Error Unavailable(string reason, string detail) => SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, reason, detail);
}
