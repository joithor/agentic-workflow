using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri services …' (API P6 unit H); stub, red.</summary>
internal sealed class ServiceVerbs
{
    internal ServiceVerbs(ControlPlaneClient client, Output output, JobFollower follower, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
    {
        Parts = (client, output, follower, ensureRunning);
    }

    private (ControlPlaneClient, Output, JobFollower, Func<bool, CancellationToken, Task<Error?>>?) Parts { get; }

    internal Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        _ = (Parts, args, interrupt, again);
        throw new NotSupportedException();
    }
}
