using Aw.Evidence.Core.Publishing;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Publishing;

/// <summary>Stub (cutover unit 1.2 fills it): gh through the package's process runner, with a timeout and an exact environment.</summary>
internal sealed class ProcessGhRunner : IGhRunner
{
    public ValueTask<Result<GhOutcome>> RunAsync(GhCall call, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the gh runner is not built yet (cutover unit 1.2)");
}
