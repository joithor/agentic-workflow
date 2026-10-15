using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneStore: the one fake, kept honest by LaneStoreContract.</summary>
public sealed class InMemoryLaneStore : ILaneStore
{
    private LaneMapState _map = LaneMapState.Empty;

    /// <inheritdoc />
    public ValueTask<Result<LaneMapState>> Current(CancellationToken cancellationToken) => Done.Of(_map);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(LaneMapState map, DateTimeOffset at, CancellationToken cancellationToken)
    {
        _map = map;
        return Done.Unit();
    }
}
