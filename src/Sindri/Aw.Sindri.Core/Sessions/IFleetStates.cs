using Aw.Kernel;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The aw:fleet hook's state files (collector spec §4.1 step 4, §5.4): the collector reads them and is the only one that prunes them.</summary>
public interface IFleetStates
{
    /// <summary>Reads every state file of the given providers' directories.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files that validate; an absent directory is empty.</returns>
    public Result<IReadOnlyList<FleetStateFile>> Read(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes dead sessions' files older than 24 hours, only in a marked provider directory and only files that validate; caps each directory at
    /// 512 files (collector spec §5.4). Never prunes under a test override.
    /// </summary>
    /// <param name="live">The sessions that are live now; their files stay.</param>
    /// <param name="now">The time to age files against.</param>
    /// <returns>The number of files deleted.</returns>
    public int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now);
}
