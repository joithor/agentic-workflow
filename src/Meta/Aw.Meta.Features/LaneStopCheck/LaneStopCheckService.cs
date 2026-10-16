using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.LaneStopCheck;

internal sealed class LaneStopCheckService : IService<LaneStopCheckRequest, LaneStopCheckResponse>
{
    public LaneStopCheckService(IMetaConfig config, ILaneMail mail, LaneBeats beats, LaneStopGuard guard, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(beats);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public ValueTask<Result<LaneStopCheckResponse>> Handle(LaneStopCheckRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
