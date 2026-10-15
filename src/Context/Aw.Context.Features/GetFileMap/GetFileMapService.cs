using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetFileMap;

/// <summary>The direct children of a repo path with their status. (S-4 stub: red until its slice is built.)</summary>
internal sealed class GetFileMapService : IService<GetFileMapRequest, FileMapView>
{
    public ValueTask<Result<FileMapView>> Handle(GetFileMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: GetFileMap is not built yet");
}
