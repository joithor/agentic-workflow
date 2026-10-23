using Aw.Kernel;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Runs the gh CLI, the way the evidence branch is written (the git data API: no checkout, no working tree touched).</summary>
public interface IGhRunner
{
    /// <summary>Runs <paramref name="call"/> to completion.</summary>
    /// <param name="call">The arguments and standard input.</param>
    /// <param name="cancellationToken">Cancels the run (gh is killed).</param>
    /// <returns>The outcome (any exit code), or AW-EVIDENCE-015 when gh would not start or ran out of time.</returns>
    public ValueTask<Result<GhOutcome>> RunAsync(GhCall call, CancellationToken cancellationToken);
}
