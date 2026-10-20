using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>'line desk step.design.check|evidence': the design station's block Steps, run at the desk.</summary>
internal sealed class DesignDeskService : IService<DesignDeskRequest, LineDeskResponse>
{
    public ValueTask<Result<LineDeskResponse>> Handle(DesignDeskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
