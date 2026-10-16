using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Runs one program (no shell) in a fresh temporary directory with exactly the environment it is given.</summary>
public interface ICliRunner
{
    /// <summary>Runs <paramref name="invocation"/> to completion.</summary>
    /// <param name="invocation">The program, arguments, input and environment.</param>
    /// <param name="cancellationToken">Cancels the run (the program is killed).</param>
    /// <returns>The outcome (any exit code), or AW-EVIDENCE-015 when the program would not start or ran out of time.</returns>
    public ValueTask<Result<CliOutcome>> RunAsync(CliInvocation invocation, CancellationToken cancellationToken);
}
