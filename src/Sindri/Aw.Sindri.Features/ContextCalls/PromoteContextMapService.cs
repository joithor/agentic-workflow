using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Promotes the latest draft (context spec §8.7), a protected command (README X30, X63): a draft with undecided review items, or no
/// draft, is SND-CONTEXT-046 before anything is asked; otherwise the preview names the draft and issues 'promote map v&lt;N&gt;' bound to
/// it; with an answer that redeems it, the engine promotes and 'context.map.promoted' is appended.
/// </summary>
internal sealed class PromoteContextMapService : IService<PromoteContextMapRequest, PromoteContextMapResponse>
{
    public ValueTask<Result<PromoteContextMapResponse>> Handle(PromoteContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
