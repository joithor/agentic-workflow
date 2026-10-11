using Aw.Evidence.Core;
using Aw.Evidence.Features.InstallBrowser;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class InstallBrowserTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task InstallBrowser_installs_through_the_installer()
    {
        var installer = new FakeBrowserInstaller();

        var result = await new InstallBrowserService(installer).Handle(new InstallBrowserRequest(), CancellationToken.None);

        Assert.Equal((true, 1), (result.Value.Installed, installer.Installs));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task InstallBrowser_reports_a_failed_install()
    {
        var result = await new InstallBrowserService(new FakeBrowserInstaller { Fails = true }).Handle(new InstallBrowserRequest(), CancellationToken.None);

        Assert.Equal(EvidenceErrors.BrowserFailed.Code, result.Error.Code);
    }
}
