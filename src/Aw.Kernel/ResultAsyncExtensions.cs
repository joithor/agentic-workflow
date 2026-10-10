namespace Aw.Kernel;

/// <summary>
/// Map and Bind over the 'ValueTask&lt;Result&lt;T&gt;&gt;' every <see cref="Services.IService{TRequest, TResponse}"/>
/// returns, so a use case can chain service calls without unwrapping each one. Each step runs only on success; an
/// error passes through unchanged. Exceptions thrown by a step propagate (they are bugs, not expected failures).
/// </summary>
public static class ResultAsyncExtensions
{
    /// <summary>Transforms the value of a success with an asynchronous step.</summary>
    /// <param name="result">The result.</param>
    /// <param name="map">The asynchronous transformation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The new value type.</typeparam>
    /// <returns>The mapped result.</returns>
    public static async ValueTask<Result<TOut>> MapAsync<T, TOut>(this Result<T> result, Func<T, ValueTask<TOut>> map) =>
        result.IsError ? Results.Failure<TOut>(result.Error) : Results.Success(await map(result.Value).ConfigureAwait(false));

    /// <summary>Chains an asynchronous operation that can itself fail.</summary>
    /// <param name="result">The result.</param>
    /// <param name="bind">The next operation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The next value type.</typeparam>
    /// <returns>The chained result.</returns>
    public static async ValueTask<Result<TOut>> BindAsync<T, TOut>(this Result<T> result, Func<T, ValueTask<Result<TOut>>> bind) =>
        result.IsError ? Results.Failure<TOut>(result.Error) : await bind(result.Value).ConfigureAwait(false);

    /// <summary>Awaits a pending result, then maps its value.</summary>
    /// <param name="pending">The pending result.</param>
    /// <param name="map">The transformation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The new value type.</typeparam>
    /// <returns>The mapped result.</returns>
    public static async ValueTask<Result<TOut>> Map<T, TOut>(this ValueTask<Result<T>> pending, Func<T, TOut> map) =>
        (await pending.ConfigureAwait(false)).Map(map);

    /// <summary>Awaits a pending result, then chains an operation that can fail.</summary>
    /// <param name="pending">The pending result.</param>
    /// <param name="bind">The next operation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The next value type.</typeparam>
    /// <returns>The chained result.</returns>
    public static async ValueTask<Result<TOut>> Bind<T, TOut>(this ValueTask<Result<T>> pending, Func<T, Result<TOut>> bind) =>
        (await pending.ConfigureAwait(false)).Bind(bind);

    /// <summary>Awaits a pending result, then maps its value with an asynchronous step.</summary>
    /// <param name="pending">The pending result.</param>
    /// <param name="map">The asynchronous transformation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The new value type.</typeparam>
    /// <returns>The mapped result.</returns>
    public static async ValueTask<Result<TOut>> MapAsync<T, TOut>(this ValueTask<Result<T>> pending, Func<T, ValueTask<TOut>> map) =>
        await (await pending.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

    /// <summary>Awaits a pending result, then chains an asynchronous operation that can fail.</summary>
    /// <param name="pending">The pending result.</param>
    /// <param name="bind">The next operation.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The next value type.</typeparam>
    /// <returns>The chained result.</returns>
    public static async ValueTask<Result<TOut>> BindAsync<T, TOut>(this ValueTask<Result<T>> pending, Func<T, ValueTask<Result<TOut>>> bind) =>
        await (await pending.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);
}
