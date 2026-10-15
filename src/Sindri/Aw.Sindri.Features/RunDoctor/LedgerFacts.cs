using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Reads what the doctor's ledger and views checks judge: the ledger's schema, its head and the projector's state.</summary>
internal sealed class LedgerFacts(ILedgerStore ledger, IViewReader views)
{
    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => ledger.Schema(cancellationToken);

    public async ValueTask<(Result<ProjectorState?> State, Result<long> Head)> Views(CancellationToken cancellationToken)
    {
        var state = await views.State(cancellationToken).ConfigureAwait(false);
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        return (state, head);
    }
}
