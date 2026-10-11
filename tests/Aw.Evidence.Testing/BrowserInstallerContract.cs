using Aw.Evidence.Core.Browser;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IBrowserInstaller contract: an install succeeds, and installing again keeps the install and succeeds.</summary>
public abstract class BrowserInstallerContract
{
    /// <summary>Creates the installer under test.</summary>
    /// <returns>The installer.</returns>
    protected abstract IBrowserInstaller Create();

    /// <summary>Installing twice succeeds both times.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Installing_twice_succeeds_both_times()
    {
        var installer = Create();

        var first = await installer.InstallAsync(CancellationToken.None);
        var second = await installer.InstallAsync(CancellationToken.None);

        Expect.That(first.IsSuccess, "the first install succeeds");
        Expect.That(second.IsSuccess, "an existing install is kept and counts as success");
    }
}
