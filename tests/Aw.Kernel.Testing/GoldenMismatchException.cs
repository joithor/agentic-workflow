namespace Aw.Kernel.Testing;

/// <summary>Output differs from its golden file after normalization, or the golden file is missing.</summary>
public sealed class GoldenMismatchException : Exception
{
    /// <summary>Creates the exception.</summary>
    public GoldenMismatchException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Where it differs.</param>
    public GoldenMismatchException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Where it differs.</param>
    /// <param name="innerException">The cause.</param>
    public GoldenMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
