using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>The scrubbed body behind one fact row (API spec section 3.8). Stub until API P11 U7.</summary>
internal sealed class GetInstanceFactService : IService<GetInstanceFactRequest, FactView>
{
    public ValueTask<Result<FactView>> Handle(GetInstanceFactRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetInstanceFact is built in API P11 U7.");
}
