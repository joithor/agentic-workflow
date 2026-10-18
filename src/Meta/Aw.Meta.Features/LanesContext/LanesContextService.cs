using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LanesContext;

/// <summary>Measures each lane's context window from the tail of its transcript and says which lanes are due for a recycle.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Measures the lanes.</param>
internal sealed class LanesContextService(IMetaConfig config, LaneContextReader reader) : IService<LanesContextRequest, LanesContextResponse>
{
    public async ValueTask<Result<LanesContextResponse>> Handle(LanesContextRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var rows = await reader.Read(team.Value, cancellationToken).ConfigureAwait(false);
        return rows.IsError
            ? rows.Error
            : new LanesContextResponse(team.Value.Context.RecycleAt, rows.Value, rows.Value.Any(static l => l.Verdict == ContextVerdict.Recycle));
    }
}
