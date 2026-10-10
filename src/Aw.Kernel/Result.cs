namespace Aw.Kernel;

/// <summary>
/// The outcome of an operation that can fail in an expected way: a value, or an <see cref="Kernel.Error"/>
/// with a catalogue code. Expected failures are never exceptions (spec §4b.3); exceptions are for bugs.
/// </summary>
/// <typeparam name="T">The success value.</typeparam>
public sealed class Result<T>
{
    internal Result(T value)
    {
        Payload = value;
    }

    internal Result(Error error)
    {
        Failure = error;
    }

    private T? Payload { get; }

    private Error? Failure { get; }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess => Failure is null;

    /// <summary>True when the operation failed with an expected error.</summary>
    public bool IsError => Failure is not null;

    /// <summary>The success value. Reading it from an error result is a bug and throws.</summary>
    public T Value => Failure is null
        ? Payload! // why: Payload is always assigned on the success path; T itself may be nullable.
        : throw new InvalidOperationException($"Result is an error ({Failure.Code}: {Failure.Message}); check IsError before reading Value.");

    /// <summary>The error. Reading it from a success result is a bug and throws.</summary>
    public Error Error => Failure ?? throw new InvalidOperationException("Result is a success; it has no Error.");

    /// <summary>A success result holding <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>A failed result holding <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    public static implicit operator Result<T>(Error error) => new(error);

    /// <summary>Transforms the value of a success; an error passes through unchanged.</summary>
    /// <param name="map">The transformation.</param>
    /// <typeparam name="TOut">The new value type.</typeparam>
    /// <returns>The mapped result.</returns>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        Failure is null ? new Result<TOut>(map(Value)) : new Result<TOut>(Failure);

    /// <summary>Chains an operation that can itself fail; an error passes through unchanged.</summary>
    /// <param name="bind">The next operation.</param>
    /// <typeparam name="TOut">The next value type.</typeparam>
    /// <returns>The chained result.</returns>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        Failure is null ? bind(Value) : new Result<TOut>(Failure);

    /// <summary>Folds the result into one value.</summary>
    /// <param name="onSuccess">Called with the value on success.</param>
    /// <param name="onError">Called with the error on failure.</param>
    /// <typeparam name="TOut">The folded type.</typeparam>
    /// <returns>Whichever branch ran.</returns>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onError) =>
        Failure is null ? onSuccess(Value) : onError(Failure);
}
