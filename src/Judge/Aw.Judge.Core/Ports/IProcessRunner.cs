using Aw.Judge.Core.Processes;

namespace Aw.Judge.Core.Ports;

/// <summary>Runs a program to completion (the agent CLIs, the keychain reader).</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the program and collects both output streams. Past <see cref="ProcessRequest.Timeout"/> the program and
    /// every process it started are stopped and the result is <see cref="ProcessEnd.TimedOut"/>; a program that can't
    /// be started is <see cref="ProcessEnd.NotStarted"/>. Neither throws.
    /// </summary>
    /// <param name="request">What to run.</param>
    /// <param name="cancellationToken">Stops the program (and its children) and throws OperationCanceledException.</param>
    /// <returns>How it ended and what it wrote.</returns>
    public ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken);
}
