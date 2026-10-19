namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// A Step input found that the data behind one of its arguments is not what the argument's reference says (a stored fact whose content no longer matches its SHA-256).
/// The fill fails with SND-WF-018 (<see cref="WorkflowErrors.InputChanged"/>): the Step must not run on data its plan does not describe. The message never carries the data.
/// </summary>
public sealed class StepInputChangedException : Exception
{
    /// <summary>Creates the exception with a message that names the argument and the reference, never the content.</summary>
    /// <param name="message">What changed.</param>
    public StepInputChangedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public StepInputChangedException()
        : base("a Step input's data changed since it was recorded")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What changed.</param>
    /// <param name="innerException">The cause.</param>
    public StepInputChangedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
