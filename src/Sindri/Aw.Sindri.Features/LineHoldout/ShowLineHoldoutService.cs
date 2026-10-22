using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LineHoldout;

/// <summary>Reads a phase's hidden scenarios for the owner, behind the typed confirmation, and marks what it showed spent (line spec §9.2).</summary>
internal sealed class ShowLineHoldoutService : IService<ShowLineHoldoutRequest, ShowLineHoldoutResponse>
{
    public ValueTask<Result<ShowLineHoldoutResponse>> Handle(ShowLineHoldoutRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
