using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A plan whose run directory and run area are real temporary directories, so a test can prepare, wrap and read what was written. Only /dev and /proc are mounted.</summary>
internal sealed class TempRunArea : IDisposable
{
    internal TempRunArea(params string[] launcherVariables)
    {
        Root = Path.Combine(Path.GetTempPath(), "aw-sb2-area-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root + "/run");
        Directory.CreateDirectory(Root + "/area");
        var built = SandboxPlannerTests.Build(Req.WriteScope);
        Plan = built with
        {
            RunDirectory = Root + "/run",
            Worktree = null,
            Mounts = [.. built.Mounts.Where(m => m.Kind is PlanMountKind.Dev or PlanMountKind.Proc)],
            Environment = [new PlanEnvVar("AW_SANDBOX_PLAN", Root + "/area/sandbox-plan.json", "test"), .. launcherVariables.Select(n => new PlanEnvVar(n, Value: null, Reason: "test"))],
        };
    }

    internal string Root { get; }

    internal SandboxPlan Plan { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
