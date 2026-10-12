using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's audit (API spec §2.1): every attempt that passes the gate gets one 'command.handled' row (accepted,
/// refused, invalid or failed) after the rest of the chain ran, plus a 'sindri.command &lt;action&gt;' span and a count.
/// An accepted self-audited row (StopProcess: its 'process.stopped' is the audit) writes none. A lost row after an
/// accepted command is logged and the result stands; on any other outcome the caller gets SND-LEDGER-002. A service
/// caller holds one context for a whole run, so each of its attempts gets its own command id here (the correlation
/// stays): a second command in the same run is a new row, not a replay of the first.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="audit">Writes the row.</param>
/// <param name="callers">The caller of this attempt.</param>
/// <param name="catalog">The command catalog the attempt is classified by.</param>
/// <param name="ids">Mints a service attempt's command id.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AuditingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, AuditTrail audit, ICallerContextAccessor callers,
    ICommandCatalog catalog, IIdGenerator ids) : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, request);
        using var attempt = EnterAttempt();
        var caller = callers.Current;
        using var span = CommandTelemetry.Source.StartActivity("sindri.command " + row.Action.Value);
        var result = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        var outcome = CommandOutcomes.Of(result.IsError ? result.Error : null);
        CommandTelemetry.Attempt(span, row.Class, caller?.Actor ?? "none", outcome);
        if (outcome == CommandOutcome.Accepted && row.SelfAudited)
        {
            return result;
        }

        var recorded = await audit.Record(caller, row, outcome, result.IsError ? result.Error.Code : null, cancellationToken).ConfigureAwait(false);
        if (recorded.IsSuccess)
        {
            return result;
        }

        if (outcome == CommandOutcome.Accepted)
        {
            audit.Lost(row, recorded.Error);
            return result;
        }

        return recorded.Error.Code == SindriErrors.LedgerUnavailable.Code ? recorded.Error
            : SindriErrors.LedgerUnavailable.With($"the audit row for {row.Action.Value} couldn't be written: {recorded.Error.Code} {recorded.Error.Message}");
    }

    /// <summary>A service caller's attempt under its own command id (null, nothing entered, for any other caller).</summary>
    private IDisposable? EnterAttempt() => callers.Current is { Class: CallerClass.Service } service
        ? callers.Enter(service with { CommandId = ids.New<CommandId>() })
        : null;
}
