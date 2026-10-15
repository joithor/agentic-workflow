using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Team;

/// <summary>Reads the team config.</summary>
/// <param name="config">Stores the config.</param>
internal sealed class TeamShowService(IMetaConfig config) : IService<TeamShowRequest, TeamConfig>
{
    public ValueTask<Result<TeamConfig>> Handle(TeamShowRequest request, CancellationToken cancellationToken) =>
        config.Load(cancellationToken);
}
