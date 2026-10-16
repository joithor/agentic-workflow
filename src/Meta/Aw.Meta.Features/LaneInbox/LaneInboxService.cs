using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.LaneInbox;

internal sealed class LaneInboxService : IService<LaneInboxRequest, LaneInboxResponse>
{
    public LaneInboxService(IMetaConfig config, ILaneMail mail, LaneBeats beats, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(beats);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public ValueTask<Result<LaneInboxResponse>> Handle(LaneInboxRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
