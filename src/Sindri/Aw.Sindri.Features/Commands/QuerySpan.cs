using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Commands;

/// <summary>Red stub: passes through until the chain lands.</summary>
/// <param name="inner">The service it wraps.</param>
/// <typeparam name="TRequest">The request record.</typeparam>
/// <typeparam name="TResponse">The response record.</typeparam>
internal sealed class QuerySpan<TRequest, TResponse>(IService<TRequest, TResponse> inner) : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => Inner.Handle(request, cancellationToken);
}
