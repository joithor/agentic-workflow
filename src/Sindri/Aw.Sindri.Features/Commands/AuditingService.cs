using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's audit (API spec §2.1): every attempt that passes the gate gets one 'command.handled' row (accepted,
/// refused, invalid or failed) after the rest of the chain ran, plus a 'sindri.command &lt;action&gt;' span and a count.
/// An accepted self-audited row (StopProcess: its 'process.stopped' is the audit) writes none. A lost row after an
/// accepted command is logged and the result stands; on any other outcome the caller gets SND-LEDGER-002.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="audit">Writes the row.</param>
/// <param name="callers">The caller of this attempt.</param>
/// <param name="catalog">The command catalog the attempt is classified by.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AuditingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, AuditTrail audit, ICallerContextAccessor callers,
    ICommandCatalog catalog) : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, request);
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
}
