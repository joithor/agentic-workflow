using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Team;

/// <summary>Lists the teams on this machine with their repo, lanes and meta session.</summary>
/// <param name="catalog">The teams on disk.</param>
internal sealed class TeamListService(ITeamCatalog catalog) : IService<TeamListRequest, TeamListResponse>
{
    public async ValueTask<Result<TeamListResponse>> Handle(TeamListRequest request, CancellationToken cancellationToken)
    {
        var teams = await catalog.List(cancellationToken).ConfigureAwait(false);
        return teams.IsError ? teams.Error : new TeamListResponse(teams.Value);
    }
}
