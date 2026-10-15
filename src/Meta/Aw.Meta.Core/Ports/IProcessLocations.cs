using Aw.Kernel;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Core.Ports;

/// <summary>Where the host's live processes are: their working directories and open files.</summary>
public interface IProcessLocations
{
    /// <summary>Lists every process this user can inspect with the paths it holds. A process that exits mid-scan is skipped; a scan that cannot run at all is an error, so a caller can fail closed.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The processes, or AW-META-010.</returns>
    public ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken);
}
