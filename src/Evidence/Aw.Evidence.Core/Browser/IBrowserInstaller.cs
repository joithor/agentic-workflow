using Aw.Kernel;

namespace Aw.Evidence.Core.Browser;

/// <summary>Installs the browser build that <see cref="IBrowser"/> drives (Chromium for the Playwright adapter).</summary>
public interface IBrowserInstaller
{
    /// <summary>Installs the browser; an existing install is kept and counts as success.</summary>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>Success, or AW-EVIDENCE-013 when the install failed.</returns>
    public ValueTask<Result<Unit>> InstallAsync(CancellationToken cancellationToken);
}
