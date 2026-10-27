#pragma warning disable MA0182 // until the launcher (SB-5a) wraps the agent in it
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Builds the systemd-run command that puts one session in its cgroup scope (sandbox spec §3.3; spike item 4). Pure.</summary>
internal static class SystemdScopeArgv
{
    /// <summary>The slice every Step's scope lives in (it nests as sindri.slice/sindri-steps.slice).</summary>
    internal const string Slice = "sindri-steps.slice";

    /// <summary>Gets the scope unit for a launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The unit name.</returns>
    internal static string UnitFor(LaunchId launch) => throw new NotSupportedException();

    /// <summary>Builds the whole command: the systemd-run program, the scope's properties, '--' and the inner command.</summary>
    /// <param name="systemdRun">The systemd-run program (the caller resolves AW_SANDBOX_SYSTEMD_RUN).</param>
    /// <param name="launch">The launch.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="inner">The command to run in the scope (the bubblewrap command).</param>
    /// <returns>The argv.</returns>
    internal static IReadOnlyList<string> Start(string systemdRun, LaunchId launch, ScopeLimits limits, IReadOnlyList<string> inner) => throw new NotSupportedException();
}
#pragma warning restore MA0182
