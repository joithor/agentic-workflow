using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.OwnerAsk;

/// <summary>Queues a question of the meta's own, asked by the meta lane (so no relay is owed when it is answered).</summary>
/// <param name="config">The team config, for the meta lane's name.</param>
/// <param name="owner">The owner's questions.</param>
/// <param name="clock">Dates the question.</param>
internal sealed class AskOwnerService(IMetaConfig config, IOwnerStore owner, TimeProvider clock) : IService<AskOwnerRequest, AskOwnerResponse>
{
    public async ValueTask<Result<AskOwnerResponse>> Handle(AskOwnerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var number = await owner.Ask(team.Value.MetaLane, request.Text.Trim(), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return number.IsError ? number.Error : new AskOwnerResponse(number.Value);
    }
}
