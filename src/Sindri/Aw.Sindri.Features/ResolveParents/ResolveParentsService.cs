using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.ResolveParents;

/// <summary>LN-PKT6: each parent hash resolves to a frozen packet, or the read is a mismatch (SND-LINE-001).</summary>
internal sealed class ResolveParentsService : IService<ResolveParentsRequest, ResolveParentsResponse>
{
    /// <summary>Holds the freeze records.</summary>
    /// <param name="frozen">The freeze records.</param>
    public ResolveParentsService(IFrozenPackets frozen)
    {
        ArgumentNullException.ThrowIfNull(frozen);
    }

    public ValueTask<Result<ResolveParentsResponse>> Handle(ResolveParentsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
