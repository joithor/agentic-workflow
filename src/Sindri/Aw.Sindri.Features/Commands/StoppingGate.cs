using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The stop's own decorator, in place of the gate (API spec §2.1): the stop runs without waiting for the gate, and once it has stopped
/// the process it cancels the command holding the gate and any that would wait for it. A long command (a 19k-entry review apply) can't
/// keep the process from stopping; a refused caller or a stop that failed cancels nothing, so a process that keeps serving keeps its gate.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="gate">The process-wide gate.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class StoppingGate<TRequest, TResponse>(IService<TRequest, TResponse> inner, CommandGateLock gate)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var stopped = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        if (stopped.IsSuccess)
        {
            gate.Stop();
        }

        return stopped;
    }
}
