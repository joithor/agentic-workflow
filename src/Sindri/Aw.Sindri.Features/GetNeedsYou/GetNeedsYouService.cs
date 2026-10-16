using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

#pragma warning disable CS9113 // Parameter is unread: the stub that the red tests compile against
internal sealed class GetNeedsYouService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetNeedsYou: make GetNeedsYouTests pass");
}
