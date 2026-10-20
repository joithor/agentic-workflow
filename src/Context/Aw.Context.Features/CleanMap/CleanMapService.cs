using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CleanMap;

/// <summary>The cleanup pass (owner F1): settles the whole review queue of the latest draft. Not built yet.</summary>
internal sealed class CleanMapService(IGraphStore store, IScrubberFactory scrubbers, IModelPort model, ICurrentUser user, TimeProvider clock) : IService<CleanMapRequest, CleanupReport>
{
    public ValueTask<Result<CleanupReport>> Handle(CleanMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("F1: CleanMapService is not built yet");
}
