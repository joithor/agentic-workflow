using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetStatus;

internal sealed class StuckInstanceCount(IEngineReader engine, TimeProvider clock)
{
    public ValueTask<int> Current(CancellationToken cancellationToken)
    {
        _ = (engine, clock, cancellationToken);
        throw new NotSupportedException();
    }
}
