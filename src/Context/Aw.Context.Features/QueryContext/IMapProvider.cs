using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>The map a query reads. The live one is the store through <see cref="MapReader"/>; a backtest arm reads a sample's as-of map.</summary>
internal interface IMapProvider
{
    /// <summary>Reads the map the caller may see.</summary>
    /// <param name="scope">The caller and the approved section.</param>
    /// <param name="read">The current map, the draft or a retained version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or the reader's error (SND-CONTEXT-030 with no map).</returns>
    public ValueTask<Result<VisibleMap>> Read(ContextScope scope, MapRead read, CancellationToken cancellationToken);
}
