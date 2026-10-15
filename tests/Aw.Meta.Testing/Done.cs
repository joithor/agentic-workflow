using Aw.Kernel;

namespace Aw.Meta.Testing;

/// <summary>Completed results for the in-memory fakes.</summary>
internal static class Done
{
    internal static ValueTask<Result<T>> Of<T>(T value) => ValueTask.FromResult(Results.Success(value));

    internal static ValueTask<Result<Unit>> Unit() => ValueTask.FromResult(Results.Ok());

    internal static ValueTask<Result<T>> Failed<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));
}
