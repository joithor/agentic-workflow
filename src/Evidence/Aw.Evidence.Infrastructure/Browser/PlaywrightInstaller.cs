using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Infrastructure.Native;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>
/// Installs the Chromium build this Playwright version drives (an existing install is kept), through Playwright's own
/// driver. The driver's progress goes to stderr (its stdout is detoured there), so the command's stdout stays one JSON
/// document. A cancellation returns at once; a download already running finishes in the driver on its own.
/// </summary>
internal sealed class PlaywrightInstaller : IBrowserInstaller
{
    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> InstallAsync(CancellationToken cancellationToken)
    {
        var exit = await Task.Run(static () =>
        {
            using (DescriptorDetour.Redirect(DescriptorDetour.StandardOutput, DescriptorDetour.StandardError))
            {
                return Microsoft.Playwright.Program.Main(["install", "chromium"]);
            }
        }, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        return exit == 0
            ? Results.Ok()
            : EvidenceErrors.BrowserFailed.With(string.Create(CultureInfo.InvariantCulture, $"installing chromium failed (playwright install exit {exit})"));
    }
}
