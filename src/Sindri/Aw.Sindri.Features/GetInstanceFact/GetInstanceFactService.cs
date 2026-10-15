using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>A stub until API P11 U5: not mapped and not registered. It will read a fact's body behind a size cap.</summary>
#pragma warning disable MA0182 // a stub until API P11 U5 registers it
internal sealed class GetInstanceFactService : IService<GetInstanceFactRequest, FactView>
{
    public ValueTask<Result<FactView>> Handle(GetInstanceFactRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("API P11 U5: GetInstanceFact is not written yet");
}
#pragma warning restore MA0182
