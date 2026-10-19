namespace Aw.Sindri.Core.Onyx;

/// <summary>What `sindri onyx up` asks the human at the terminal: to press Enter, for an Onyx login. Nothing that is read here is ever echoed, logged or stored (the password least of all).</summary>
public interface IOnyxPrompts
{
    /// <summary>Gets a value indicating whether a person can answer: standard input and output are a terminal.</summary>
    public bool Interactive { get; }

    /// <summary>Shows a message and waits for Enter.</summary>
    /// <param name="message">What to tell the human to do first.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends when the human pressed Enter.</returns>
    public ValueTask Pause(string message, CancellationToken cancellationToken);

    /// <summary>Asks a question; the answer is shown as it is typed.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The trimmed answer, or null when input ended.</returns>
    public ValueTask<string?> Ask(string question, CancellationToken cancellationToken);

    /// <summary>Asks for a secret with echo off.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer, untrimmed, or null when input ended.</returns>
    public ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken);
}
