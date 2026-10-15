using Aw.Kernel;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>The lane map and its version history.</summary>
public interface ILaneStore
{
    /// <summary>The current map.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The map; empty with version 0 before any was saved.</returns>
    public ValueTask<Result<LaneMapState>> Current(CancellationToken cancellationToken);

    /// <summary>Makes <paramref name="map"/> current and records its version.</summary>
    /// <param name="map">The map.</param>
    /// <param name="at">When it changed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Save(LaneMapState map, DateTimeOffset at, CancellationToken cancellationToken);
}
