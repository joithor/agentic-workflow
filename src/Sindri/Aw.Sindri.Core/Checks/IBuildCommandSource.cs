using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Checks;

/// <summary>Where the build and ship stations get the repo's commands (line spec §7.2, LN-WF9).</summary>
public interface IBuildCommandSource
{
    /// <summary>The commands for a stage, in order.</summary>
    /// <param name="stage">'red', 'checks' or 'green' (<see cref="BuildCommand.Where"/>).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commands; none when none are configured for the stage.</returns>
    ValueTask<Result<IReadOnlyList<BuildCommand>>> For(string stage, CancellationToken cancellationToken);
}
