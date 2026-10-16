using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetLaunch;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class GetLaunchService : IService<GetLaunchRequest, GetLaunchResponse>
{
    public ValueTask<Result<GetLaunchResponse>> Handle(GetLaunchRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetLaunch: make GetLaunchTests pass");
}
