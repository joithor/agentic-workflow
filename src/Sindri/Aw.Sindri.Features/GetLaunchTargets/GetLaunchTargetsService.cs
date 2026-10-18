using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.GetLaunchTargets;

/// <summary>The last recorded probe with today's budgets (API spec 'GET /launch/targets').</summary>
/// <param name="reader">Reads the probe and the budgets.</param>
internal sealed class GetLaunchTargetsService(LaunchTargetsReader reader) : IService<GetLaunchTargetsRequest, LaunchTargets>
{
    public ValueTask<Result<LaunchTargets>> Handle(GetLaunchTargetsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return reader.Read(cancellationToken);
    }
}
