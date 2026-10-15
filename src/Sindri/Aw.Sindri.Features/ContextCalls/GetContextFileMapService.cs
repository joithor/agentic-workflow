using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The direct children of a repo path with their status, through the engine; a repo name that can't be one is SND-CONTEXT-045 like any repo outside the scope.</summary>
internal sealed class GetContextFileMapService : IService<GetContextFileMapRequest, FileMapView>
{
    public ValueTask<Result<FileMapView>> Handle(GetContextFileMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
