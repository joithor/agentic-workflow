using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.LogAppend;

internal sealed class AppendLogService : IService<AppendLogRequest, AppendLogResponse>
{
    public ValueTask<Result<AppendLogResponse>> Handle(AppendLogRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
