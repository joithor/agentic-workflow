using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version and its counts.</summary>
internal sealed class ShowMapStatusService : IService<ShowMapStatusRequest, MapStatusView>
{
    public ValueTask<Result<MapStatusView>> Handle(ShowMapStatusRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
