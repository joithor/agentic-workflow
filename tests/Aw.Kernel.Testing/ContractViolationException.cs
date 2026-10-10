namespace Aw.Kernel.Testing;

/// <summary>A contract test found an implementation that breaks the port's contract.</summary>
public sealed class ContractViolationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ContractViolationException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What broke.</param>
    public ContractViolationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What broke.</param>
    /// <param name="innerException">The cause.</param>
    public ContractViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
