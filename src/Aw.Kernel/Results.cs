namespace Aw.Kernel;

/// <summary>Factories for <see cref="Result{T}"/> when a conversion can't be inferred.</summary>
public static class Results
{
    /// <summary>A success result holding <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A success result.</returns>
    public static Result<T> Success<T>(T value) => new(value);

    /// <summary>A failed result holding <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <typeparam name="T">The value type the caller expected.</typeparam>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure<T>(Error error) => new(error);

    /// <summary>A success with no value, for operations that only succeed or fail.</summary>
    /// <returns>A success result holding <see cref="Unit.Value"/>.</returns>
    public static Result<Unit> Ok() => new(Unit.Value);
}
