using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>The in-memory browser installer: counts installs; can be told to fail.</summary>
public sealed class FakeBrowserInstaller : IBrowserInstaller
{
    /// <summary>When true, every install fails with AW-EVIDENCE-013.</summary>
    public bool Fails { get; set; }

    /// <summary>How many installs succeeded.</summary>
    public int Installs { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> InstallAsync(CancellationToken cancellationToken)
    {
        if (Fails)
        {
            return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.BrowserFailed.With("browser install failed"));
        }

        Installs++;
        return ValueTask.FromResult(Results.Ok());
    }
}
