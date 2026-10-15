using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;
/// <summary>The lenses of one anchor, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextLensesService : IService<GetContextLensesRequest, LensesView>
{
    public ValueTask<Result<LensesView>> Handle(GetContextLensesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
