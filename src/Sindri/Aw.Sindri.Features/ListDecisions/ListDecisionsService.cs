using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListDecisions;

#pragma warning disable CS9113 // Parameter is unread: the stub that the red tests compile against
internal sealed class ListDecisionsService(IViewReader views, IItemResolver items, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListDecisionsRequest, ListDecisionsResponse>
{
    public ValueTask<Result<ListDecisionsResponse>> Handle(ListDecisionsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListDecisions: make ListDecisionsTests pass");
}
