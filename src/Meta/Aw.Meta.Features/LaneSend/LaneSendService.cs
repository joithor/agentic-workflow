using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.LaneSend;

internal sealed class LaneSendService : IService<LaneSendRequest, LaneSendResponse>
{
    public LaneSendService(IMetaConfig config, ILaneFiles files, ILaneMail mail, IMessageStore messages, LaneBeats beats, ILanePane pane, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(beats);
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public ValueTask<Result<LaneSendResponse>> Handle(LaneSendRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
