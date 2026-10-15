using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Features.Team;

internal sealed class TeamInitService : IService<TeamInitRequest, TeamConfig>
{
    public ValueTask<Result<TeamConfig>> Handle(TeamInitRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
