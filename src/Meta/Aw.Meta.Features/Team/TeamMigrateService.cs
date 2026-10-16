using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Team;

/// <summary>Moves the legacy flat layout into a named team (opt-in; nothing calls it but the team-migrate command).</summary>
/// <param name="catalog">The teams on disk.</param>
internal sealed class TeamMigrateService(ITeamCatalog catalog) : IService<TeamMigrateRequest, TeamMigration>
{
    public ValueTask<Result<TeamMigration>> Handle(TeamMigrateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return catalog.Migrate(request.DryRun, cancellationToken);
    }
}
