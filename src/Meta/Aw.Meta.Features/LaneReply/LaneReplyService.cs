using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneReply;

internal sealed class LaneReplyService : IService<LaneReplyRequest, LaneReplyResponse>
{
    public LaneReplyService(IMetaConfig config, ILaneFiles files, ILaneMail mail, IRunningLog log, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public ValueTask<Result<LaneReplyResponse>> Handle(LaneReplyRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
