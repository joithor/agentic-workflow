using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetNeedsYou;

internal sealed class GetNeedsYouService : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetNeedsYou: make GetNeedsYouTests pass");
}
