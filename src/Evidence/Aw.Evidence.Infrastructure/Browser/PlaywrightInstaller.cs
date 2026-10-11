using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Infrastructure.Native;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>Installs the Chromium build this Playwright version drives (an existing install is kept), through Playwright's own driver.</summary>
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
        }, cancellationToken).ConfigureAwait(false);
        return exit == 0
            ? Results.Ok()
            : EvidenceErrors.BrowserFailed.With(string.Create(CultureInfo.InvariantCulture, $"installing chromium failed (playwright install exit {exit})"));
    }
}
