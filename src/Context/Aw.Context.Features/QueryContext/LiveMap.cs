using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>The live map: what <see cref="MapReader"/> gives the caller.</summary>
internal sealed class LiveMap(MapReader reader) : IMapProvider
{
    public ValueTask<Result<VisibleMap>> Read(ContextScope scope, MapRead read, CancellationToken cancellationToken) => reader.ReadAt(scope, read, cancellationToken);
}
