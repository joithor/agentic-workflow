using System.Text;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Makes the host side of a plan exist before bubblewrap starts (a bind needs its source): the per-launch and scratch directories, the empty global git config,
/// the refusing pre-push hook and a placeholder plan file. Later steps fill the shims (SB-3b) and the plan file (SB-5a); this only makes the stack start.
/// </summary>
internal static class RunAreaPreparer
{
    private const string RefusingPrePush = "#!/bin/sh\necho 'git push is not available in a sandboxed session: Sindri sends pushes and pull requests for you.' >&2\nexit 1\n";

    /// <summary>Creates what the plan's required binds need and does not yet exist.</summary>
    /// <param name="plan">The plan.</param>
    internal static void Prepare(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var area = RunArea(plan);
        foreach (var mount in plan.Mounts.Where(m => m is { Optional: false, Kind: PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir }))
        {
            _ = Directory.CreateDirectory(mount.Source ?? mount.Path);
        }

        if (area is null)
        {
            return;
        }

        _ = Directory.CreateDirectory(area + "/shims");
        _ = Directory.CreateDirectory(area + "/githooks");
        WriteIfAbsent(area + "/githooks/pre-push", RefusingPrePush, executable: true);
        WriteIfAbsent(area + "/gitconfig.empty", string.Empty, executable: false);
        WriteIfAbsent(area + "/sandbox-plan.json", "{}\n", executable: false);
    }

    // The plan file sits in the run area, and AW_SANDBOX_PLAN names it.
    private static string? RunArea(SandboxPlan plan) =>
        plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "AW_SANDBOX_PLAN", StringComparison.Ordinal))?.Value is { } file ? Path.GetDirectoryName(file) : null;

    private static void WriteIfAbsent(string path, string content, bool executable)
    {
        if (File.Exists(path))
        {
            return;
        }

        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | (executable ? UnixFileMode.UserExecute : UnixFileMode.None);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
