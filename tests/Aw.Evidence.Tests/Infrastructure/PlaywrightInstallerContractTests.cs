using Aw.Evidence.Core.Browser;
using Aw.Evidence.Infrastructure.Browser;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real installer (in the shared-browser collection, after the fixture has installed Chromium once).</summary>
[Lane(TestLane.Heavy)]
[Collection(SharedBrowser.Name)]
public sealed class PlaywrightInstallerContractTests : BrowserInstallerContract
{
    protected override IBrowserInstaller Create() => new PlaywrightInstaller();
}
