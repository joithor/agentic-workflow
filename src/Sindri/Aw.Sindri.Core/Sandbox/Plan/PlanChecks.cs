using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The checks that decide whether a request can be planned at all: the paths (-015), the socket path (-016) and the prepared login (-016, -017's neighbours).</summary>
internal static class PlanChecks
{
    private const int SocketPathLimit = 104;

    private static readonly string[] _renameWritten = [".credentials.json", ".claude.json", "auth.json"];

    internal static Error? Validate(PlanContext c) => Worktree(c) ?? Socket(c) ?? Login(c);

    private static Error? Worktree(PlanContext c)
    {
        var paths = c.Request.Paths;
        if (paths.Worktree is { } worktree && !worktree.StartsWith(paths.Repo.TrimEnd('/') + "/", StringComparison.Ordinal))
        {
            return SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.WorktreeOutsideRepo, "the worktree is not inside the repo");
        }

        return c.WritablePath is { } writable && !string.Equals(writable, paths.Worktree, StringComparison.Ordinal)
            ? SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.WorktreeOutsideRepo, "the writable path is not the run's worktree")
            : null;
    }

    private static Error? Socket(PlanContext c) =>
        Encoding.UTF8.GetByteCount(c.Request.Net.NetDir + "/sindri.sock") >= SocketPathLimit
            ? SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the socket directory is too long for a unix socket path")
            : null;

    private static Error? Login(PlanContext c)
    {
        if (c.Request.Login is not { } login)
        {
            return null;
        }

        if (login.Source != c.Login)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Login, "the prepared login is not from the source the plan resolved");
        }

        return LoginMount(c, login) ?? LoginEnvironment(login);
    }

    private static Error? LoginMount(PlanContext c, PreparedLogin login)
    {
        var roots = new[] { c.Home + "/.claude", c.Home + "/.codex" };
        foreach (var mount in login.Mounts)
        {
            if (_renameWritten.Any(f => mount.SandboxPath.EndsWith("/" + f, StringComparison.Ordinal)))
            {
                return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Login, "a login file is rewritten by rename: bind its directory, not the file");
            }

            if (mount.Mode == MountMode.ReadWrite && roots.Contains(mount.SandboxPath, StringComparer.Ordinal))
            {
                return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Login, "the agent home must be read-only");
            }
        }

        return null;
    }

    private static Error? LoginEnvironment(PreparedLogin login) =>
        login.Environment.Keys.FirstOrDefault(n => !EnvironmentAllowlist.IsListed(n)) is { } name
            ? SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Env, $"the login sets {name}, which is not on the environment allowlist")
            : null;
}
