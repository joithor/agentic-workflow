using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Init;

internal sealed class InitService : IService<InitRequest, InitResponse>
{
    public ValueTask<Result<InitResponse>> Handle(InitRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Init: make InitTests pass");
}
