using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The launcher service's start-up sweep (<see cref="ILaunchStartup"/>): the viewer files an earlier run left behind.</summary>
/// <param name="sweepWarp">Sweeps Warp's launch directory.</param>
internal sealed class LaunchStartup(Action sweepWarp) : ILaunchStartup
{
    /// <inheritdoc/>
    public void Sweep()
    {
        try
        {
            sweepWarp();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a directory that can't be listed leaves its files for the next start; the launcher still runs
        }
    }
}
