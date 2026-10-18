using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'scoping desk': a block Step's code, run at the desk (scoping spec §8.2).</summary>
internal sealed class ScopingDeskService : IService<ScopingDeskRequest, ScopingDeskResponse>
{
    public ValueTask<Result<ScopingDeskResponse>> Handle(ScopingDeskRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ScopingDeskService is not built yet");
}
