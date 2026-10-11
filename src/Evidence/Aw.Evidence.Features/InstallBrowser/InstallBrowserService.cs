using Aw.Evidence.Core.Browser;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.InstallBrowser;

/// <summary>Installs the browser through the installer port.</summary>
internal sealed class InstallBrowserService(IBrowserInstaller installer) : IService<InstallBrowserRequest, InstallBrowserResponse>
{
    public async ValueTask<Result<InstallBrowserResponse>> Handle(InstallBrowserRequest request, CancellationToken cancellationToken)
    {
        var installed = await installer.InstallAsync(cancellationToken).ConfigureAwait(false);
        return installed.Map(static _ => new InstallBrowserResponse(Installed: true));
    }
}
