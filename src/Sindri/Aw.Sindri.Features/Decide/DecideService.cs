using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Decide;

#pragma warning disable CS9113 // Parameter is unread: the stub that the red tests compile against
internal sealed class DecideService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers)
    : IService<DecideRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(DecideRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Decide: make DecideTests pass");
}
