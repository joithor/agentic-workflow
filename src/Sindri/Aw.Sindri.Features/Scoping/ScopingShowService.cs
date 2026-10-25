using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'scoping show': the stop view over the facts a caller holds (scoping spec §8.4).</summary>
internal sealed class ScopingShowService : IService<ScopingShowRequest, ScopingView>
{
    public ValueTask<Result<ScopingView>> Handle(ScopingShowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<ScopingView>>(ScopingViewBuilder.Build(new ScopeFacts(request.Facts)));
    }
}
