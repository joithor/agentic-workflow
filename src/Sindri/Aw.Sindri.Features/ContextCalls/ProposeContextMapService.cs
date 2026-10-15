using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Writes a scoping run's surfaces to the latest draft (context spec §8.5; README X63): the engine adds 'propose' rows (idempotent,
/// never again after a reject, never the current map), and 'context.map.proposed' is appended when something was added. A job-class
/// command run synchronously: it is fast, and the app refreshes on the event.
/// </summary>
internal sealed class ProposeContextMapService : IService<ProposeContextMapRequest, ProposeContextMapResponse>
{
    public ValueTask<Result<ProposeContextMapResponse>> Handle(ProposeContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
