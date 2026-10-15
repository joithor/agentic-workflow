using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Team;

/// <summary>Writes a starter team config: the lanes, the meta lane and a gate with one 'repo' scope that matches every path.</summary>
/// <param name="config">Stores the config.</param>
internal sealed class TeamInitService(IMetaConfig config) : IService<TeamInitRequest, TeamConfig>
{
    public async ValueTask<Result<TeamConfig>> Handle(TeamInitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await config.Load(cancellationToken).ConfigureAwait(false);
        if (!request.Force && (existing.IsSuccess || existing.Error.Code != MetaErrors.NoTeamConfig.Code))
        {
            return existing.IsSuccess
                ? MetaErrors.ConfigExists.With("a team config already exists; pass --force to replace it")
                : existing.Error;
        }

        var lanes = request.Lanes.Contains(request.MetaLane, StringComparer.OrdinalIgnoreCase) ? request.Lanes : [request.MetaLane, .. request.Lanes];
        var created = new TeamConfig(request.Team, request.Repo, lanes, request.MetaLane, request.Log, gate: Starter());
        var saved = await config.Save(created, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : created;
    }

    private static GateConfig Starter() =>
        new(["repo"], [], new Dictionary<string, ScopeConfig>(StringComparer.Ordinal) { ["repo"] = new(["**"], []) });
}
