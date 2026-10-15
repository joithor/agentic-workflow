using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Features.Team;

internal sealed class TeamShowService : IService<TeamShowRequest, TeamConfig>
{
    public ValueTask<Result<TeamConfig>> Handle(TeamShowRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
