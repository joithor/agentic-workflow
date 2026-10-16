using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri jobs …' (API P6 unit H); stub, red.</summary>
internal sealed class JobVerbs
{
    internal JobVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
    {
        Parts = (client, output, ensureRunning);
    }

    private (ControlPlaneClient, Output, Func<bool, CancellationToken, Task<Error?>>?) Parts { get; }

    internal Task<int> Run(string[] args, CancellationToken ct)
    {
        _ = (Parts, args, ct);
        throw new NotSupportedException();
    }
}
