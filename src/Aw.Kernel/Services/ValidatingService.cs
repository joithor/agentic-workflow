namespace Aw.Kernel.Services;

/// <summary>Runs a validator before the wrapped service; an invalid request never reaches it.</summary>
/// <param name="inner">The service to protect.</param>
/// <param name="validator">The request's validator.</param>
/// <typeparam name="TRequest">The request record.</typeparam>
/// <typeparam name="TResponse">The response record.</typeparam>
public sealed class ValidatingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, IValidator<TRequest> validator)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <inheritdoc />
    public override ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var valid = validator.Validate(request);
        return valid.IsError
            ? ValueTask.FromResult<Result<TResponse>>(valid.Error)
            : Inner.Handle(request, cancellationToken);
    }
}
