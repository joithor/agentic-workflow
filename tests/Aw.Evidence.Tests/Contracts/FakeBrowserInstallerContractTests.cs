using Aw.Evidence.Core.Browser;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeBrowserInstallerContractTests : BrowserInstallerContract
{
    protected override IBrowserInstaller Create() => new FakeBrowserInstaller();
}
