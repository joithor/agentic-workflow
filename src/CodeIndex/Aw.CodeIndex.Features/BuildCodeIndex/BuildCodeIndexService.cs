using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

internal sealed class BuildCodeIndexService : IService<BuildCodeIndexRequest, BuildCodeIndexResponse>
{
    public ValueTask<Result<BuildCodeIndexResponse>> Handle(BuildCodeIndexRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("BuildCodeIndex: make BuildCodeIndexTests pass");
}
