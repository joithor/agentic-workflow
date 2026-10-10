namespace Aw.Kernel.Services;

/// <summary>Validates a request at a boundary (an endpoint or a CLI input), before the service runs.</summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Checks <paramref name="value"/>.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns>Success, or an <see cref="ErrorKind.Invalid"/> error that says what is wrong.</returns>
    public Result<Unit> Validate(T value);
}
