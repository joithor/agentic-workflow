using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'scoping check': the pure rules over the facts a caller holds (scoping spec §8.2).</summary>
internal sealed class ScopingCheckService : IService<ScopingCheckRequest, ScopingCheckResponse>
{
    public ValueTask<Result<ScopingCheckResponse>> Handle(ScopingCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<ScopingCheckResponse>>(ScopePacketChecks.Run(new ScopeFacts(request.Facts), request.Stage));
    }
}
