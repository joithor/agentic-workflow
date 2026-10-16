using Aw.Kernel;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>The lane records under lane-sessions: the session id a CLI's hook reported and the agent facts the launcher recorded.</summary>
public interface ILaneFiles
{
    /// <summary>Reads a lane's record.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null when the launcher left nothing for the lane. A file that can't be read counts as absent.</returns>
    public ValueTask<Result<LaneRecord?>> Record(string lane, CancellationToken cancellationToken);
}
