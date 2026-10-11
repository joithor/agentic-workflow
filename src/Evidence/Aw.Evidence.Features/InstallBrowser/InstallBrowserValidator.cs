using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.InstallBrowser;

internal sealed class InstallBrowserValidator : IValidator<InstallBrowserRequest>
{
    public Result<Unit> Validate(InstallBrowserRequest value) => Unit.Value;
}
