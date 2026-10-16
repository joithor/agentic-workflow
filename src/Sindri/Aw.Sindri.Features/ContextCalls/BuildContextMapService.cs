using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a build job: builds the draft through the engine under the starting caller and records 'context.map.built'.</summary>
internal sealed class BuildContextMapService : IService<BuildContextMapRequest, BuildContextMapResult>
{
    public ValueTask<Result<BuildContextMapResult>> Handle(BuildContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
