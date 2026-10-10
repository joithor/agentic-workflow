using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetHealth;

internal sealed class GetHealthService : IService<GetHealthRequest, GetHealthResponse>
{
    public ValueTask<Result<GetHealthResponse>> Handle(GetHealthRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetHealth: make GetHealthTests pass");
}
