using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The stop's own decorator, in place of the gate (API spec §2.1): once the caller is authorized and the request valid, it cancels the
/// command holding the gate, and any that would wait for it, then runs the stop. A long command (a 19k-entry review apply) can't keep the
/// process from stopping, and a refused caller cancels nothing.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="gate">The process-wide gate.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class StoppingGate<TRequest, TResponse>(IService<TRequest, TResponse> inner, CommandGateLock gate)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    public override ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        gate.Stop();
        return Inner.Handle(request, cancellationToken);
    }
}
