using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckRetention;

/// <summary>Doctor 'context:retention'.</summary>
internal sealed class CheckRetentionService : IService<CheckRetentionRequest, MapHealth>
{
    public ValueTask<Result<MapHealth>> Handle(CheckRetentionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
