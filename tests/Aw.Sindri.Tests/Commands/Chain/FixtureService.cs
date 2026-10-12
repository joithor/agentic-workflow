using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The fixture handler: decides from the request and appends 'fixture.done' under the lease, like a real command.</summary>
internal sealed class FixtureService(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers, FixtureLatch latch) : IService<FixtureCommand, CommandAccepted>
{
    public const string Done = "fixture.done";

    public async ValueTask<Result<CommandAccepted>> Handle(FixtureCommand request, CancellationToken cancellationToken)
    {
        latch.Start();
        if (request.Outcome == "block")
        {
            await latch.Release.Task.WaitAsync(cancellationToken);
        }

        if (request.Outcome == "fail")
        {
            return SindriErrors.ProfileHashMismatch.With("changed since you looked");
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"{Done}:{latch.Started}");
        var appended = await ledger.Append(events.Entry(Done, new ProcessStopped(1, 1, StopReason.Stop), key),
            held.Current!, cancellationToken);   // why: the chain tests hold the lease before an accepted run
        return appended.IsError ? appended.Error
            : new CommandAccepted(callers.Current!.CommandId, appended.Value.Sequence);   // why: authorization let it through, so a caller is set
    }
}
