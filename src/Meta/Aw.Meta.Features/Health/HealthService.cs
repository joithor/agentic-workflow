using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Health;

internal sealed class HealthService : IService<HealthRequest, HealthResponse>
{
    public ValueTask<Result<HealthResponse>> Handle(HealthRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
