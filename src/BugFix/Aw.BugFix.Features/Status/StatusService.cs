using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Status;

internal sealed class StatusService(IStateStore store) : IService<StatusRequest, BugFixState>
{
    public ValueTask<Result<BugFixState>> Handle(StatusRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory));
}
