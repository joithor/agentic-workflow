using Aw.BugFix.Core.Model;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Status;

internal sealed class StatusService : IService<StatusRequest, BugFixState>
{
    public ValueTask<Result<BugFixState>> Handle(StatusRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Status: make StatusTests pass");
}
