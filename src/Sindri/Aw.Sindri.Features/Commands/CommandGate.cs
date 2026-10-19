using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's outermost decorator (API spec §2.1): serializes every command through the <see cref="CommandGateLock"/>.
/// A command that waits longer than 5 s fails with SND-API-009 and never reaches the audit, so it writes nothing.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="gate">The process-wide gate.</param>
/// <param name="clock">The clock the 5 s limit runs on.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class CommandGate<TRequest, TResponse>(IService<TRequest, TResponse> inner, CommandGateLock gate, TimeProvider clock)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        using var running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gate.Stopping);   // a stop cancels the holder
        var waiting = clock.GetTimestamp();
        var entered = await gate.Enter(clock, running.Token).ConfigureAwait(false);
        CommandTelemetry.GateWait.Record(clock.GetElapsedTime(waiting).TotalSeconds);
        if (!entered)
        {
            return SindriErrors.CommandBusy.With("another command held the command gate for 5 s; try again in a moment");
        }

        try
        {
            return await Inner.Handle(request, running.Token).ConfigureAwait(false);
        }
        finally
        {
            gate.Exit();
        }
    }
}
