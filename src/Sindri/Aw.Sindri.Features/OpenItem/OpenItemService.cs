using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.OpenItem;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class OpenItemService : IService<OpenItemRequest, OpenItemResponse>
{
    public ValueTask<Result<OpenItemResponse>> Handle(OpenItemRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("OpenItem: make OpenItemTests pass");
}
