using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>The teams on this machine: listing them and moving the legacy flat layout into a named team.</summary>
public interface ITeamCatalog
{
    /// <summary>Lists every team: the named ones, and the legacy flat one.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The teams, by name.</returns>
    public ValueTask<Result<IReadOnlyList<TeamSummary>>> List(CancellationToken cancellationToken);

    /// <summary>Moves the legacy flat layout into teams/&lt;its team name&gt;; opt-in, never automatic.</summary>
    /// <param name="dryRun">Report only.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What moved, AW-META-002 when there is no legacy config, or AW-META-020 when it can't be moved.</returns>
    public ValueTask<Result<TeamMigration>> Migrate(bool dryRun, CancellationToken cancellationToken);
}
