namespace Aw.Kernel.Services;

/// <summary>
/// One use case (spec §4b.2). Endpoints and CLI commands bind input, call one service and map its result;
/// cross-cutting behaviour wraps it as a <see cref="Decorated{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The request record.</typeparam>
/// <typeparam name="TResponse">The response record.</typeparam>
public interface IService<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Runs the use case.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The response, or an expected error.</returns>
    public ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken);
}
