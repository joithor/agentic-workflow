namespace Aw.Kernel.Services;

/// <summary>
/// Base for cross-cutting behaviour around a service: validation, logging, transactions, budgets (spec §4b.2).
/// Decorators are registered explicitly in the owning project's Add…() method, outermost first; there is no
/// MediatR-style pipeline.
/// </summary>
/// <param name="inner">The service this decorator wraps.</param>
/// <typeparam name="TRequest">The request record.</typeparam>
/// <typeparam name="TResponse">The response record.</typeparam>
public abstract class Decorated<TRequest, TResponse>(IService<TRequest, TResponse> inner) : IService<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>The wrapped service.</summary>
    protected IService<TRequest, TResponse> Inner { get; } = inner;

    /// <inheritdoc />
    public abstract ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken);
}
