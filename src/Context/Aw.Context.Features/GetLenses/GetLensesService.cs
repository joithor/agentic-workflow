using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetLenses;

/// <summary>The intent, product and code bands around one node. (S-4 stub: red until its slice is built.)</summary>
internal sealed class GetLensesService : IService<GetLensesRequest, LensesView>
{
    public ValueTask<Result<LensesView>> Handle(GetLensesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: GetLenses is not built yet");
}
